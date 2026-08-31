namespace Backend.Services;

public sealed record ProductionRequirement(long MaterialId, long VersionId, decimal Quantity);

public sealed record IncomingMaterialSupply(decimal Quantity, DateOnly ExpectedDate);

public sealed record PlanningMaterial(long MaterialId, string Name, string Type, long? CurrentVersionId)
{
    public decimal AvailableQuantity { get; init; }
    public decimal ReservedQuantity { get; init; }
    public decimal SafetyStock { get; init; }
    public IReadOnlyList<IncomingMaterialSupply> Incoming { get; init; } = [];
}

public sealed record PlanningBomComponent(
    long ParentMaterialId, long ChildMaterialId, decimal Quantity, decimal LossRate);

public sealed record MaterialPlanningSnapshot(
    IReadOnlyDictionary<long, PlanningMaterial> Materials,
    IReadOnlyDictionary<long, long> VersionOwners,
    IReadOnlyDictionary<long, IReadOnlyList<PlanningBomComponent>> ComponentsByVersion);

public sealed record MaterialRequirementPlan(
    IReadOnlyList<MaterialRequirementNettingItem> Items, DateOnly LatestSupplyDate)
{
    public MaterialReadiness GetMaterialReadiness()
    {
        // Intermediate shortages are production requirements, satisfied by their own components.
        var shortages = Items.Where(item => item.IsLeaf && item.NetRequirement > 0)
            .Select(item => $"物料 {item.MaterialId} 缺少 {item.NetRequirement:0.####}").ToList();
        return shortages.Count == 0
            ? new MaterialReadiness(true, LatestSupplyDate, null)
            : new MaterialReadiness(false, null, string.Join("；", shortages));
    }
}

public sealed class MaterialPlanningException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

/// <summary>
/// Allocates shared supply before exploding replenishment demand. Root quantities are firm
/// production quantities; only their components are netted against inventory.
/// </summary>
public static class MaterialRequirementPlanner
{
    public static MaterialRequirementPlan Calculate(
        IReadOnlyCollection<ProductionRequirement> requests,
        MaterialPlanningSnapshot snapshot,
        bool includeSafetyStock,
        DateOnly today)
    {
        var components = new Dictionary<long, IReadOnlyList<PlanningBomComponent>>();
        var demands = new Dictionary<long, PlanningDemand>();

        foreach (var request in requests)
        {
            if (request.MaterialId <= 0 || request.VersionId <= 0 || request.Quantity <= 0)
                throw new MaterialPlanningException(400, "物料、版本编号和生产数量必须大于 0");
            GetMaterial(snapshot, request.MaterialId);
            if (!snapshot.VersionOwners.TryGetValue(request.VersionId, out var owner) || owner != request.MaterialId)
                throw new MaterialPlanningException(400, "BOM 版本不属于指定物料");

            // Validate the complete reachable BOM, including branches covered by stock.
            DiscoverComponents(snapshot, request.MaterialId, request.VersionId, [], components);
            Propagate(demands, request.MaterialId, request.Quantity, 0, [request.MaterialId.ToString()],
                GetComponents(snapshot, request.MaterialId, request.VersionId));
        }

        // A shared component can occur at several depths. Wait for ALL parents rather than
        // sorting by minimum depth, or its stock/safety stock could be allocated more than once.
        var pendingParents = components.Keys.ToDictionary(id => id, _ => 0);
        foreach (var edges in components.Values)
            foreach (var childId in edges.Select(edge => edge.ChildMaterialId).Distinct())
                pendingParents[childId]++;
        var ready = new SortedSet<long>(pendingParents.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var results = new List<MaterialRequirementNettingItem>();
        var latestSupplyDate = today;
        var processed = 0;

        while (ready.Count > 0)
        {
            var materialId = ready.Min;
            ready.Remove(materialId);
            processed++;
            var edges = components[materialId];
            if (demands.TryGetValue(materialId, out var demand))
            {
                var material = GetMaterial(snapshot, materialId);
                var safetyStock = includeSafetyStock ? material.SafetyStock : 0;
                var remaining = demand.GrossQuantity + safetyStock
                    - material.AvailableQuantity - material.ReservedQuantity;
                var incoming = material.Incoming.Where(supply => supply.Quantity > 0)
                    .OrderBy(supply => supply.ExpectedDate).ToList();
                foreach (var supply in incoming)
                {
                    if (remaining <= 0) break;
                    remaining -= supply.Quantity;
                    if (supply.ExpectedDate > latestSupplyDate) latestSupplyDate = supply.ExpectedDate;
                }
                var netRequirement = decimal.Ceiling(Math.Max(remaining, 0) * 100) / 100;
                results.Add(new MaterialRequirementNettingItem(
                    materialId, material.Name, material.Type,
                    demand.Parents.Count == 1 ? demand.Parents.Single() : null,
                    demand.Depth, demand.NetQuantity, demand.GrossQuantity,
                    material.AvailableQuantity, incoming.Sum(supply => supply.Quantity), safetyStock,
                    netRequirement, demand.LossRates.OrderBy(rate => rate).ToList(),
                    string.Join(" | ", demand.Paths.OrderBy(path => path, StringComparer.Ordinal)),
                    edges.Count == 0));

                if (netRequirement > 0)
                    Propagate(demands, materialId, netRequirement, demand.Depth, demand.Paths, edges);
            }

            foreach (var childId in edges.Select(edge => edge.ChildMaterialId).Distinct())
                if (--pendingParents[childId] == 0) ready.Add(childId);
        }

        if (processed != components.Count)
            throw new MaterialPlanningException(409, "BOM 存在循环依赖，无法展开需求");

        return new MaterialRequirementPlan(
            results.OrderBy(item => item.Depth).ThenBy(item => item.MaterialId).ToList(), latestSupplyDate);
    }

    private static PlanningMaterial GetMaterial(MaterialPlanningSnapshot snapshot, long materialId) =>
        snapshot.Materials.TryGetValue(materialId, out var material)
            ? material
            : throw new MaterialPlanningException(404, $"物料 {materialId} 不存在");

    private static IReadOnlyList<PlanningBomComponent> GetComponents(
        MaterialPlanningSnapshot snapshot, long materialId, long? versionId) =>
        versionId.HasValue && snapshot.ComponentsByVersion.TryGetValue(versionId.Value, out var edges)
            ? edges.Where(edge => edge.ParentMaterialId == materialId).ToList()
            : [];

    private static void DiscoverComponents(
        MaterialPlanningSnapshot snapshot,
        long materialId,
        long? versionId,
        HashSet<long> path,
        Dictionary<long, IReadOnlyList<PlanningBomComponent>> components)
    {
        if (!path.Add(materialId))
            throw new MaterialPlanningException(409, $"BOM 存在循环依赖，涉及物料 {materialId}");

        foreach (var edge in GetComponents(snapshot, materialId, versionId))
        {
            if (edge.Quantity <= 0 || edge.LossRate < 0 || edge.LossRate >= 1)
                throw new MaterialPlanningException(409, "BOM 用量或损耗率无效");
            var child = GetMaterial(snapshot, edge.ChildMaterialId);
            components.TryAdd(child.MaterialId, GetComponents(snapshot, child.MaterialId, child.CurrentVersionId));
            DiscoverComponents(snapshot, child.MaterialId, child.CurrentVersionId, path, components);
        }
        path.Remove(materialId);
    }

    private static void Propagate(
        Dictionary<long, PlanningDemand> demands,
        long parentId,
        decimal quantity,
        int depth,
        IEnumerable<string> paths,
        IReadOnlyList<PlanningBomComponent> components)
    {
        foreach (var edge in components)
        {
            if (!demands.TryGetValue(edge.ChildMaterialId, out var demand))
            {
                demand = new PlanningDemand();
                demands.Add(edge.ChildMaterialId, demand);
            }
            var net = quantity * edge.Quantity;
            demand.NetQuantity += net;
            demand.GrossQuantity += decimal.Ceiling(net / (1 - edge.LossRate));
            demand.Depth = Math.Min(demand.Depth, depth + 1);
            demand.Parents.Add(parentId);
            demand.LossRates.Add(edge.LossRate);
            foreach (var path in paths) demand.Paths.Add($"{path}/{edge.ChildMaterialId}");
        }
    }

    private sealed class PlanningDemand
    {
        public decimal NetQuantity { get; set; }
        public decimal GrossQuantity { get; set; }
        public int Depth { get; set; } = int.MaxValue;
        public HashSet<long> Parents { get; } = [];
        public HashSet<decimal> LossRates { get; } = [];
        public HashSet<string> Paths { get; } = new(StringComparer.Ordinal);
    }
}
