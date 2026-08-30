using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

public sealed record MaterialRequirementNettingItem(
    long MaterialId,
    string MaterialName,
    string MaterialType,
    long? ParentMaterialId,
    int Depth,
    decimal BomNetQuantity,
    decimal GrossRequirement,
    decimal AvailableQuantity,
    decimal InTransitQuantity,
    decimal SafetyStock,
    decimal NetRequirement,
    IReadOnlyList<decimal> LossRates,
    string Path,
    bool IsLeaf);

/// <summary>
/// Nets expanded BOM demand against shared stock, in-transit supply, and safety stock.
/// </summary>
public sealed class MaterialRequirementNettingService
{
    public IReadOnlyList<MaterialRequirementNettingItem> Calculate(
        OracleConnection connection,
        OracleTransaction? transaction,
        IReadOnlyCollection<BomDemandExpansionItem> expandedItems)
    {
        var results = new List<MaterialRequirementNettingItem>();
        var demandGroups = expandedItems
            .GroupBy(item => item.MaterialId)
            .OrderBy(group => group.Min(item => item.Depth))
            .ThenBy(group => group.Key);

        foreach (var demandGroup in demandGroups)
        {
            var representative = demandGroup
                .OrderBy(item => item.Depth)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .First();
            var combinedPath = string.Join(
                " | ",
                demandGroup
                    .SelectMany(item => item.Path.Split(
                        " | ",
                        StringSplitOptions.RemoveEmptyEntries))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal));
            var rates = demandGroup.Select(item => item.LossRate).Distinct().ToList();
            var grossRequirement = demandGroup.Sum(item => item.GrossQuantity);
            var availableQuantity = GetAvailableQuantity(connection, transaction, demandGroup.Key);
            var inTransitQuantity = GetInTransitQuantity(connection, transaction, demandGroup.Key);
            var safetyStock = GetSafetyStock(connection, transaction, demandGroup.Key);
            var netRequirement = Math.Max(
                grossRequirement - availableQuantity - inTransitQuantity + safetyStock,
                0);
            netRequirement = Math.Ceiling(netRequirement * 100) / 100;

            results.Add(new MaterialRequirementNettingItem(
                demandGroup.Key,
                representative.MaterialName,
                representative.MaterialType,
                demandGroup.Any(item => item.Depth == 0) ? null : ResolveParentId(combinedPath),
                demandGroup.Min(item => item.Depth),
                demandGroup.Sum(item => item.NetQuantity),
                grossRequirement,
                availableQuantity,
                inTransitQuantity,
                safetyStock,
                netRequirement,
                rates.OrderBy(rate => rate).ToList(),
                combinedPath,
                demandGroup.All(item => item.IsLeaf)));
        }

        return results;
    }

    private static decimal GetAvailableQuantity(
        OracleConnection connection,
        OracleTransaction? transaction,
        long materialId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT AVAILABLE_QTY FROM MATERIAL_STOCK WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToDecimal(value);
    }

    private static decimal GetInTransitQuantity(
        OracleConnection connection,
        OracleTransaction? transaction,
        long materialId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"SELECT COALESCE(SUM(poi.QUANTITY - NVL(poi.RECEIVED_QTY, 0)), 0)
                            FROM PURCHASE_ORDER_ITEM poi
                            JOIN PURCHASE_ORDER po ON po.ORDER_ID = poi.ORDER_ID
                            WHERE poi.MATERIAL_ID = :materialId
                              AND po.STATUS IN (:submitted, :partial)";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("submitted", PurchaseOrderStatusMap.Db.Submitted));
        cmd.Parameters.Add(new OracleParameter("partial", PurchaseOrderStatusMap.Db.PartialReceived));
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    private static decimal GetSafetyStock(
        OracleConnection connection,
        OracleTransaction? transaction,
        long materialId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT SAFETY_STOCK FROM MATERIAL WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToDecimal(value);
    }

    private static long? ResolveParentId(string path)
    {
        var parents = path.Split(" | ", StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Select(parts => parts.Length >= 2 ? parts[^2] : null)
            .Where(parent => parent is not null)
            .Select(parent => parent!)
            .Distinct()
            .ToList();
        return parents.Count == 1 && long.TryParse(parents[0], out var parentId)
            ? parentId
            : null;
    }
}
