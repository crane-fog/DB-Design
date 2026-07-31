using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

/// <summary>
/// B 模块对 A 的报价契约实现：仅取物料默认供应商在 pricingDate 生效的报价，
/// 多条有效报价取 valid_from 最新的一条；无默认供应商或默认供应商无有效报价时返回 Missing。
/// </summary>
public sealed class PriceQueryService(string connString) : IPriceQuery
{
    public IReadOnlyDictionary<long, EffectivePriceResult> GetEffectivePrices(
        IReadOnlyCollection<long> materialIds, DateOnly pricingDate)
    {
        var results = materialIds.Distinct().ToDictionary(
            id => id,
            id => new EffectivePriceResult(id, null, null, null, null, true, "未配置默认供应商"));
        if (results.Count == 0)
        {
            return results;
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        var names = results.Keys.Select((_, index) => $":materialId{index}").ToArray();
        cmd.CommandText = $@"SELECT m.MATERIAL_ID, m.DEFAULT_SUPPLIER_ID, sp.PRICE, sp.VALID_FROM, sp.VALID_TO
                             FROM MATERIAL m
                             LEFT JOIN SUPPLIER_PRICE sp
                               ON sp.SUPPLIER_ID = m.DEFAULT_SUPPLIER_ID
                              AND sp.MATERIAL_ID = m.MATERIAL_ID
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

        var index = 0;
        foreach (var materialId in results.Keys)
        {
            cmd.Parameters.Add(new OracleParameter($"materialId{index}", materialId));
            index++;
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = Convert.ToInt64(reader.GetValue(0));
            if (reader.IsDBNull(1))
            {
                continue;
            }

            var supplierId = Convert.ToInt64(reader.GetValue(1));
            results[id] = reader.IsDBNull(2)
                ? new EffectivePriceResult(id, supplierId, null, null, null, true, "默认供应商没有有效报价")
                : new EffectivePriceResult(id, supplierId, reader.GetDecimal(2),
                    DateOnly.FromDateTime(reader.GetDateTime(3)),
                    reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
                    false, null);
        }
        return results;
    }
}
