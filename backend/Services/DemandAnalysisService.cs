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
        try
        {
            using var command = OracleCommandFactory.Create(
                conn,
                "BEGIN PKG_BOM_DOMAIN.OPEN_DEMAND(:materialId, :versionId, :quantity, :rows); END;",
                transaction);
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
            command.Parameters.Add("versionId", OracleDbType.Int64).Value = versionId;
            command.Parameters.Add("quantity", OracleDbType.Decimal).Value = quantity;
            command.Parameters.Add("rows", OracleDbType.RefCursor).Direction =
                System.Data.ParameterDirection.Output;

            var results = new List<BomDemandExpansionItem>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new BomDemandExpansionItem(
                    Convert.ToInt64(reader.GetValue(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.GetDecimal(7),
                    Convert.ToInt32(reader.GetValue(4)),
                    reader.GetString(8),
                    Convert.ToInt32(reader.GetValue(9)) == 1));
            }

            return results;
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            throw new DemandAnalysisBusinessException((DemandAnalysisError)code, message);
        }
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

    private sealed class DemandAnalysisBusinessException(DemandAnalysisError error, string message) : Exception(message) { public DemandAnalysisError Error { get; } = error; }
}
