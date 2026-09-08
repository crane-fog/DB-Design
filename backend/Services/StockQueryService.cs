using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

/// <summary>
/// B 模块对 A 的库存契约实现：批量读取库存快照，并在调用方事务内幂等创建零库存记录。
/// </summary>
public sealed class StockQueryService(string connString) : IStockReadQuery, IStockInitialization
{
    public IReadOnlyDictionary<long, StockSnapshot> GetSnapshots(IReadOnlyCollection<long> materialIds)
    {
        if (materialIds.Count == 0)
        {
            return new Dictionary<long, StockSnapshot>();
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var names = materialIds.Select((_, index) => $":id{index}").ToArray();
        cmd.CommandText = $@"SELECT MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY, LAST_IN_DATE, LAST_OUT_DATE
                             FROM MATERIAL_STOCK
                             WHERE MATERIAL_ID IN ({string.Join(", ", names)})";

        var index = 0;
        foreach (var materialId in materialIds)
        {
            cmd.Parameters.Add(new OracleParameter($"id{index}", materialId));
            index++;
        }

        var result = new Dictionary<long, StockSnapshot>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var materialId = Convert.ToInt64(reader.GetValue(0));
            result[materialId] = new StockSnapshot(
                materialId,
                reader.GetDecimal(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetUtcDateTime(3),
                reader.IsDBNull(4) ? null : reader.GetUtcDateTime(4));
        }
        return result;
    }

    public void EnsureStockRecord(OracleConnection connection, OracleTransaction transaction, long materialId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"MERGE INTO MATERIAL_STOCK target
                            USING (SELECT :materialId AS MATERIAL_ID FROM DUAL) source
                            ON (target.MATERIAL_ID = source.MATERIAL_ID)
                            WHEN NOT MATCHED THEN
                                INSERT (MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY)
                                VALUES (source.MATERIAL_ID, 0, 0)";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.ExecuteNonQuery();
    }
}
