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

public sealed record MaterialPlanningOptions(
    bool IncludeSafetyStock = true, long? OrderId = null, bool RequireEffectiveChildVersions = true);

/// <summary>Loads a read-only planning snapshot in the caller's transaction.</summary>
public sealed class MaterialRequirementNettingService
{
    private const string OrderReservationsSql = @"SELECT MATERIAL_ID, SUM(LOCK_QTY) FROM STOCK_LOCK
              WHERE ORDER_ID = :orderId AND STATUS = :locked
              GROUP BY MATERIAL_ID";

    public MaterialRequirementPlan Calculate(
        OracleConnection connection,
        OracleTransaction? transaction,
        IReadOnlyCollection<ProductionRequirement> requests,
        MaterialPlanningOptions? options = null)
    {
        options ??= new MaterialPlanningOptions();
        var materials = LoadMaterials(connection, transaction, options.RequireEffectiveChildVersions);
        var versions = new Dictionary<long, long>();
        using (var cmd = OracleCommandFactory.Create(connection,
            "SELECT VERSION_ID, MATERIAL_ID FROM BOM_VERSION", transaction))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                versions.Add(Convert.ToInt64(reader.GetValue(0)), Convert.ToInt64(reader.GetValue(1)));
        }

        var components = new Dictionary<long, List<PlanningBomComponent>>();
        using (var cmd = OracleCommandFactory.Create(connection,
            @"SELECT VERSION_ID, PARENT_MATERIAL_ID, CHILD_MATERIAL_ID, QUANTITY, LOSS_RATE
              FROM BOM ORDER BY BOM_ID", transaction))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var versionId = Convert.ToInt64(reader.GetValue(0));
                if (!components.TryGetValue(versionId, out var edges))
                {
                    edges = [];
                    components.Add(versionId, edges);
                }
                edges.Add(new PlanningBomComponent(Convert.ToInt64(reader.GetValue(1)),
                    Convert.ToInt64(reader.GetValue(2)), reader.GetDecimal(3), reader.GetDecimal(4)));
            }
        }

        LoadIncomingSupply(connection, transaction, materials);
        if (options.OrderId.HasValue) LoadOrderReservations(connection, transaction, options.OrderId.Value, materials);
        var snapshot = new MaterialPlanningSnapshot(materials, versions,
            components.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<PlanningBomComponent>)pair.Value));
        return MaterialRequirementPlanner.Calculate(requests, snapshot, options.IncludeSafetyStock,
            DateOnly.FromDateTime(DateTime.Today));
    }

    private static Dictionary<long, PlanningMaterial> LoadMaterials(
        OracleConnection connection, OracleTransaction? transaction, bool requireEffectiveChildVersions)
    {
        using var cmd = OracleCommandFactory.Create(connection,
            @"SELECT m.MATERIAL_ID, m.MATERIAL_NAME, m.MATERIAL_TYPE, m.CURRENT_VERSION_ID,
                     effective_bv.VERSION_ID, NVL(ms.AVAILABLE_QTY, 0), NVL(m.SAFETY_STOCK, 0)
              FROM MATERIAL m
              LEFT JOIN MATERIAL_STOCK ms ON ms.MATERIAL_ID = m.MATERIAL_ID
              LEFT JOIN BOM_VERSION effective_bv
                ON effective_bv.VERSION_ID = m.CURRENT_VERSION_ID
               AND effective_bv.EFFECTIVE_DATE <= TRUNC(SYSDATE)
               AND (effective_bv.EXPIRE_DATE IS NULL OR effective_bv.EXPIRE_DATE >= TRUNC(SYSDATE))", transaction);
        var result = new Dictionary<long, PlanningMaterial>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = Convert.ToInt64(reader.GetValue(0));
            var versionColumn = requireEffectiveChildVersions ? 4 : 3;
            result.Add(id, new PlanningMaterial(id, reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(versionColumn) ? null : Convert.ToInt64(reader.GetValue(versionColumn)))
            {
                AvailableQuantity = reader.GetDecimal(5),
                SafetyStock = reader.GetDecimal(6)
            });
        }
        return result;
    }

    private static void LoadIncomingSupply(
        OracleConnection connection, OracleTransaction? transaction, Dictionary<long, PlanningMaterial> materials)
    {
        using var cmd = OracleCommandFactory.Create(connection,
            @"SELECT poi.MATERIAL_ID, poi.QUANTITY - NVL(poi.RECEIVED_QTY, 0), po.EXPECTED_DATE
              FROM PURCHASE_ORDER_ITEM poi
              JOIN PURCHASE_ORDER po ON po.ORDER_ID = poi.ORDER_ID
              WHERE po.STATUS IN (:submitted, :partial)
                AND poi.QUANTITY > NVL(poi.RECEIVED_QTY, 0)
              ORDER BY po.EXPECTED_DATE, poi.ITEM_ID", transaction);
        cmd.Parameters.Add("submitted", OracleDbType.Varchar2).Value = PurchaseOrderStatusMap.Db.Submitted;
        cmd.Parameters.Add("partial", OracleDbType.Varchar2).Value = PurchaseOrderStatusMap.Db.PartialReceived;
        var incoming = new Dictionary<long, List<IncomingMaterialSupply>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = Convert.ToInt64(reader.GetValue(0));
            if (!incoming.TryGetValue(id, out var supply))
            {
                supply = [];
                incoming.Add(id, supply);
            }
            supply.Add(new IncomingMaterialSupply(reader.GetDecimal(1), DateOnly.FromDateTime(reader.GetDateTime(2))));
        }
        foreach (var (id, supply) in incoming)
            if (materials.TryGetValue(id, out var material)) materials[id] = material with { Incoming = supply };
    }

    private static void LoadOrderReservations(
        OracleConnection connection, OracleTransaction? transaction, long orderId,
        Dictionary<long, PlanningMaterial> materials)
    {
        // AVAILABLE_QTY already excludes every reservation. Only this order's still-active locks
        // can be added back; MATERIAL_STOCK.LOCKED_QTY also includes stock owned by other orders.
        using var cmd = OracleCommandFactory.Create(connection, OrderReservationsSql, transaction);
        cmd.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        cmd.Parameters.Add("locked", OracleDbType.Varchar2).Value = StockLockStatusMap.Db.Locked;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = Convert.ToInt64(reader.GetValue(0));
            if (materials.TryGetValue(id, out var material))
                materials[id] = material with { ReservedQuantity = reader.GetDecimal(1) };
        }
    }
}
