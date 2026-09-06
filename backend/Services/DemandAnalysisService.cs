using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public enum DemandAnalysisError
{
    BadRequest = 400,
    NotFound = 404,
    Conflict = 409,
}

public sealed record DemandAnalysisResult<T>(bool Ok, T? Data, DemandAnalysisError Error, string? ErrorMessage)
{
    public static DemandAnalysisResult<T> Success(T data) => new(true, data, 0, null);
    public static DemandAnalysisResult<T> Fail(DemandAnalysisError error, string message) => new(false, default, error, message);
}

/// <summary>Temporary A-side price implementation, replaceable by B through DI.</summary>
public sealed class SupplierPriceIntegrationService(string connString) : IPriceQuery
{
    public IReadOnlyDictionary<long, EffectivePriceResult> GetEffectivePrices(
        IReadOnlyCollection<long> materialIds, DateOnly pricingDate)
    {
        var results = materialIds.Distinct().ToDictionary(
            id => id, id => new EffectivePriceResult(id, null, null, null, null, true, "未配置默认供应商"));
        if (results.Count == 0) return results;

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        var names = results.Keys.Select((_, index) => $":materialId{index}").ToArray();
        cmd.CommandText = $@"SELECT m.MATERIAL_ID, m.DEFAULT_SUPPLIER_ID, sp.PRICE, sp.VALID_FROM, sp.VALID_TO
                             FROM MATERIAL m
                             LEFT JOIN SUPPLIER_PRICE sp
                               ON sp.SUPPLIER_ID = m.DEFAULT_SUPPLIER_ID AND sp.MATERIAL_ID = m.MATERIAL_ID
                              AND sp.VALID_FROM <= :pricingDate
                              AND (sp.VALID_TO IS NULL OR sp.VALID_TO >= :pricingDate)
                              AND sp.VALID_FROM = (
                                  SELECT MAX(candidate.VALID_FROM) FROM SUPPLIER_PRICE candidate
                                  WHERE candidate.SUPPLIER_ID = m.DEFAULT_SUPPLIER_ID
                                    AND candidate.MATERIAL_ID = m.MATERIAL_ID
                                    AND candidate.VALID_FROM <= :pricingDate
                                    AND (candidate.VALID_TO IS NULL OR candidate.VALID_TO >= :pricingDate))
                             WHERE m.MATERIAL_ID IN ({string.Join(", ", names)})";
        cmd.Parameters.Add(new OracleParameter("pricingDate", pricingDate.ToDateTime(TimeOnly.MinValue)));
        foreach (var (materialId, index) in results.Keys.Select((id, index) => (id, index)))
            cmd.Parameters.Add(new OracleParameter($"materialId{index}", materialId));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = Convert.ToInt64(reader.GetValue(0));
            if (reader.IsDBNull(1)) continue;
            var supplierId = Convert.ToInt64(reader.GetValue(1));
            results[id] = reader.IsDBNull(2)
                ? new EffectivePriceResult(id, supplierId, null, null, null, true, "默认供应商没有有效报价")
                : new EffectivePriceResult(id, supplierId, reader.GetDecimal(2), DateOnly.FromDateTime(reader.GetDateTime(3)),
                    reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)), false, null);
        }
        return results;
    }
}

public sealed class DemandAnalysisService(
    string connString,
    IPriceQuery priceQuery) : IBomExpansionQuery
{
    public DemandAnalysisResult<List<LossCompensationItem>> CalculateLossCompensation(LossCompensationCalculateRequest request)
    {
        var error = ValidateInput(request.MaterialId, request.VersionId, request.NetQuantity, "净需求量");
        if (error is not null) return DemandAnalysisResult<List<LossCompensationItem>>.Fail(DemandAnalysisError.BadRequest, error);
        try
        {
            return DemandAnalysisResult<List<LossCompensationItem>>.Success(ExpandDemand(request.MaterialId, request.VersionId, Convert.ToDecimal(request.NetQuantity))
                .Select(item => new LossCompensationItem
                {
                    MaterialId = Convert.ToInt32(item.MaterialId),
                    MaterialName = item.MaterialName,
                    NetQuantity = decimal.ToDouble(item.NetQuantity),
                    LossRate = decimal.ToDouble(item.LossRate),
                    GrossQuantity = decimal.ToDouble(item.GrossQuantity)
                }).ToList());
        }
        catch (DemandAnalysisBusinessException ex) { return DemandAnalysisResult<List<LossCompensationItem>>.Fail(ex.Error, ex.Message); }
    }

    public DemandAnalysisResult<ProductCostResult> CalculateProductCost(ProductCostCalculateRequest request)
    {
        var error = ValidateInput(request.MaterialId, request.VersionId, request.ProductionQty, "生产数量");
        if (error is not null) return DemandAnalysisResult<ProductCostResult>.Fail(DemandAnalysisError.BadRequest, error);
        try
        {
            var leaves = ExpandDemand(request.MaterialId, request.VersionId, Convert.ToDecimal(request.ProductionQty)).Where(item => item.IsLeaf).ToList();
            var prices = priceQuery.GetEffectivePrices(leaves.Select(item => item.MaterialId).ToArray(), GetDatabaseDate());
            var missing = leaves.Where(item => !prices.TryGetValue(item.MaterialId, out var price) || price.Missing || !price.Price.HasValue)
                .Select(item => $"{item.MaterialName}（{item.MaterialId}）").Distinct().ToList();
            if (missing.Count > 0)
                return DemandAnalysisResult<ProductCostResult>.Fail(DemandAnalysisError.Conflict, $"以下叶子物料缺少默认供应商的有效报价：{string.Join("、", missing)}");

            var items = leaves.Select(item =>
            {
                var price = prices[item.MaterialId].Price!.Value;
                return new ProductCostItem
                {
                    MaterialId = Convert.ToInt32(item.MaterialId),
                    MaterialName = item.MaterialName,
                    NetQuantity = decimal.ToDouble(item.NetQuantity),
                    GrossQuantity = decimal.ToDouble(item.GrossQuantity),
                    UnitPrice = decimal.ToDouble(price),
                    Amount = decimal.ToDouble(item.GrossQuantity * price)
                };
            }).ToList();
            return DemandAnalysisResult<ProductCostResult>.Success(new ProductCostResult
            {
                MaterialId = request.MaterialId,
                VersionId = request.VersionId,
                TotalCost = decimal.ToDouble(items.Sum(item => Convert.ToDecimal(item.Amount))),
                Items = items
            });
        }
        catch (DemandAnalysisBusinessException ex) { return DemandAnalysisResult<ProductCostResult>.Fail(ex.Error, ex.Message); }
    }

    public IReadOnlyList<BomDemandExpansionItem> ExpandDemand(long materialId, long versionId, decimal quantity, OracleConnection? connection = null, OracleTransaction? transaction = null)
    {
        var error = ValidateInput(materialId, versionId, quantity, "数量");
        if (error is not null) throw new DemandAnalysisBusinessException(DemandAnalysisError.BadRequest, error);
        using var ownedConnection = connection is null ? new OracleConnection(connString) : null;
        var conn = connection ?? ownedConnection!;
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        var graph = LoadGraph(conn, transaction);
        if (!graph.Materials.TryGetValue(materialId, out var root)) throw new DemandAnalysisBusinessException(DemandAnalysisError.NotFound, "物料不存在");
        if (!graph.VersionMaterialIds.TryGetValue(versionId, out var ownerId) || ownerId != materialId)
            throw new DemandAnalysisBusinessException(DemandAnalysisError.BadRequest, "BOM 版本不属于指定物料");

        var occurrences = new List<BomDemandExpansionItem>();
        ExpandGraph(graph, root, versionId, quantity, 0, materialId.ToString(), new HashSet<long> { materialId }, occurrences);
        return occurrences.GroupBy(item => new { item.MaterialId, item.LossRate }).Select(group =>
        {
            var first = group.OrderBy(item => item.Depth).ThenBy(item => item.Path, StringComparer.Ordinal).First();
            return first with
            {
                NetQuantity = group.Sum(item => item.NetQuantity),
                GrossQuantity = group.Sum(item => item.GrossQuantity),
                Depth = group.Min(item => item.Depth),
                Path = string.Join(" | ", group.Select(item => item.Path).Distinct().OrderBy(path => path, StringComparer.Ordinal)),
                IsLeaf = group.All(item => item.IsLeaf)
            };
        }).OrderBy(item => item.Depth).ThenBy(item => item.MaterialId).ThenBy(item => item.LossRate).ToList();
    }

    private DateOnly GetDatabaseDate()
    {
        using var conn = new OracleConnection(connString); conn.Open(); using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)) FROM DUAL";
        return DateOnly.FromDateTime(Convert.ToDateTime(cmd.ExecuteScalar()));
    }
    private static string? ValidateInput(long materialId, long versionId, double quantity, string name) =>
        !double.IsFinite(quantity) ? $"{name}必须是有效数字" : ValidateInput(materialId, versionId, Convert.ToDecimal(quantity), name);
    private static string? ValidateInput(long materialId, long versionId, decimal quantity, string name) =>
        materialId <= 0 || versionId <= 0 ? "物料和版本编号不能为空" : quantity <= 0 ? $"{name}必须大于 0" : null;

    private static DemandGraph LoadGraph(OracleConnection conn, OracleTransaction? transaction)
    {
        using var cmd = conn.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = @"SELECT m.MATERIAL_ID, m.MATERIAL_NAME, m.MATERIAL_TYPE, current_bv.VERSION_ID, bv.VERSION_ID, bv.MATERIAL_ID,
                                   b.PARENT_MATERIAL_ID, b.CHILD_MATERIAL_ID, b.VERSION_ID, b.QUANTITY, b.LOSS_RATE
                            FROM MATERIAL m
                            LEFT JOIN BOM_VERSION current_bv
                              ON current_bv.VERSION_ID = m.CURRENT_VERSION_ID
                             AND current_bv.EFFECTIVE_DATE <= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                             AND (current_bv.EXPIRE_DATE IS NULL OR current_bv.EXPIRE_DATE >= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
                            LEFT JOIN BOM_VERSION bv ON bv.MATERIAL_ID = m.MATERIAL_ID
                            LEFT JOIN BOM b ON b.VERSION_ID = bv.VERSION_ID
                            ORDER BY m.MATERIAL_ID, bv.VERSION_ID, b.BOM_ID";
        var materials = new Dictionary<long, DemandMaterial>(); var versions = new Dictionary<long, long>(); var children = new Dictionary<long, List<DemandEdge>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var materialId = Convert.ToInt64(reader.GetValue(0));
            materials.TryAdd(materialId, new DemandMaterial(materialId, reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : Convert.ToInt64(reader.GetValue(3))));
            if (reader.IsDBNull(4)) continue;
            var versionId = Convert.ToInt64(reader.GetValue(4)); versions.TryAdd(versionId, Convert.ToInt64(reader.GetValue(5)));
            if (reader.IsDBNull(6)) continue;
            if (!children.TryGetValue(versionId, out var edges)) { edges = []; children[versionId] = edges; }
            edges.Add(new DemandEdge(Convert.ToInt64(reader.GetValue(6)), Convert.ToInt64(reader.GetValue(7)), reader.GetDecimal(9), reader.GetDecimal(10)));
        }
        return new DemandGraph(materials, versions, children);
    }
    private static void ExpandGraph(DemandGraph graph, DemandMaterial parent, long versionId, decimal parentGross, int parentDepth, string parentPath, IReadOnlySet<long> path, List<BomDemandExpansionItem> results)
    {
        if (!graph.ChildrenByVersion.TryGetValue(versionId, out var edges)) return;
        foreach (var edge in edges.Where(edge => edge.ParentMaterialId == parent.MaterialId))
        {
            if (!graph.Materials.TryGetValue(edge.ChildMaterialId, out var child)) continue;
            if (path.Contains(child.MaterialId)) throw new DemandAnalysisBusinessException(DemandAnalysisError.Conflict, "BOM 存在循环依赖，无法展开需求");
            var net = parentGross * edge.Quantity;
            var gross = decimal.Ceiling(net / (1 - edge.LossRate));
            var isLeaf = !child.CurrentVersionId.HasValue || !graph.ChildrenByVersion.TryGetValue(child.CurrentVersionId.Value, out var childEdges) || childEdges.All(e => e.ParentMaterialId != child.MaterialId);
            var itemPath = $"{parentPath}/{child.MaterialId}";
            results.Add(new BomDemandExpansionItem(child.MaterialId, child.MaterialName, child.MaterialType, net, gross, edge.LossRate, parentDepth + 1, itemPath, isLeaf));
            if (!isLeaf) ExpandGraph(graph, child, child.CurrentVersionId!.Value, gross, parentDepth + 1, itemPath, new HashSet<long>(path) { child.MaterialId }, results);
        }
    }
    private sealed record DemandGraph(Dictionary<long, DemandMaterial> Materials, Dictionary<long, long> VersionMaterialIds, Dictionary<long, List<DemandEdge>> ChildrenByVersion);
    private sealed record DemandMaterial(long MaterialId, string MaterialName, string MaterialType, long? CurrentVersionId);
    private sealed record DemandEdge(long ParentMaterialId, long ChildMaterialId, decimal Quantity, decimal LossRate);
    private sealed class DemandAnalysisBusinessException(DemandAnalysisError error, string message) : Exception(message) { public DemandAnalysisError Error { get; } = error; }
}
