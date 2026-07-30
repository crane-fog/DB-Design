using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed class CapacityEstimationDataSource(string connString)
{
    public ProductionResourceResult<CapacityEstimateInput> ResolveInput(
        ProductionCapacityEstimateRequest request)
    {
        try
        {
            using OracleConnection connection = OpenConnection();
            CapacityEstimateInput input;

            if (request.OrderId > 0)
            {
                using OracleCommand command = OracleCommandFactory.Create(
                    connection,
                    @"SELECT MATERIAL_ID, VERSION_ID, PLAN_QTY, PLAN_END
                      FROM PRODUCTION_ORDER
                      WHERE ORDER_ID = :orderId");
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                using OracleDataReader reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return ProductionResourceResult<CapacityEstimateInput>.Fail(
                        404,
                        "生产订单不存在");
                }

                input = new CapacityEstimateInput(
                    Convert.ToInt64(reader.GetValue(0)),
                    Convert.ToInt64(reader.GetValue(1)),
                    Convert.ToDecimal(reader.GetValue(2)),
                    DateOnly.FromDateTime(reader.GetDateTime(3)));
            }
            else
            {
                if (request.MaterialId <= 0
                    || request.VersionId <= 0
                    || request.PlanQty <= 0
                    || request.ExpectedDate == default)
                {
                    return ProductionResourceResult<CapacityEstimateInput>.Fail(
                        400,
                        "临时估算必须提供产品、BOM 版本、数量和期望日期");
                }

                input = new CapacityEstimateInput(
                    request.MaterialId,
                    request.VersionId,
                    request.PlanQty,
                    request.ExpectedDate);
            }

            if (!BomVersionMatches(connection, input.VersionId, input.MaterialId))
            {
                return ProductionResourceResult<CapacityEstimateInput>.Fail(
                    404,
                    "BOM 版本不存在或与产品不匹配");
            }

            return ProductionResourceResult<CapacityEstimateInput>.Success(input);
        }
        catch (OracleException)
        {
            return ProductionResourceResult<CapacityEstimateInput>.Fail(
                500,
                "读取产能估算基础数据失败");
        }
    }

    public ProductionResourceResult<MaterialReadiness> EvaluateMaterialReadiness(
        CapacityEstimateInput input)
    {
        try
        {
            using OracleConnection connection = OpenConnection();
            Dictionary<long, decimal> requirements = [];
            HashSet<long> path = [];
            string? expansionError = ExpandRequirements(
                connection,
                input.MaterialId,
                input.VersionId,
                input.PlanQty,
                path,
                requirements);
            if (expansionError is not null)
            {
                return ProductionResourceResult<MaterialReadiness>.Fail(409, expansionError);
            }

            if (requirements.Count == 0)
            {
                return ProductionResourceResult<MaterialReadiness>.Success(
                    new MaterialReadiness(true, DateOnly.FromDateTime(DateTime.Today), null));
            }

            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            DateOnly latestReadyDate = today;
            List<string> shortages = [];

            foreach ((long materialId, decimal requiredQuantity) in requirements)
            {
                decimal remaining = requiredQuantity - ReadAvailableQuantity(connection, materialId);
                if (remaining <= 0)
                {
                    continue;
                }

                DateOnly? materialReadyDate = null;
                foreach (IncomingMaterial incoming in ReadIncomingMaterials(connection, materialId))
                {
                    remaining -= incoming.Quantity;
                    materialReadyDate = incoming.ExpectedDate < today ? today : incoming.ExpectedDate;
                    if (remaining <= 0)
                    {
                        break;
                    }
                }

                if (remaining > 0)
                {
                    shortages.Add($"物料 {materialId} 缺少 {remaining:0.####}");
                    continue;
                }

                if (materialReadyDate.HasValue && materialReadyDate.Value > latestReadyDate)
                {
                    latestReadyDate = materialReadyDate.Value;
                }
            }

            if (shortages.Count > 0)
            {
                return ProductionResourceResult<MaterialReadiness>.Success(
                    new MaterialReadiness(false, null, string.Join("；", shortages)));
            }

            return ProductionResourceResult<MaterialReadiness>.Success(
                new MaterialReadiness(true, latestReadyDate, null));
        }
        catch (OracleException)
        {
            return ProductionResourceResult<MaterialReadiness>.Fail(
                500,
                "计算物料齐套情况失败");
        }
    }

    private string? ExpandRequirements(
        OracleConnection connection,
        long materialId,
        long versionId,
        decimal parentQuantity,
        HashSet<long> path,
        Dictionary<long, decimal> requirements)
    {
        if (!path.Add(materialId))
        {
            return $"BOM 存在循环依赖，涉及物料 {materialId}";
        }

        List<BomComponent> children = ReadBomComponents(connection, materialId, versionId);
        if (children.Count == 0)
        {
            path.Remove(materialId);
            return null;
        }

        foreach (BomComponent child in children)
        {
            decimal grossQuantity = decimal.Ceiling(
                parentQuantity * child.Quantity / (1 - child.LossRate));
            long? childVersionId = ReadCurrentVersionId(connection, child.MaterialId);
            bool hasChildBom = childVersionId.HasValue
                && HasBomComponents(connection, child.MaterialId, childVersionId.Value);

            if (hasChildBom)
            {
                string? error = ExpandRequirements(
                    connection,
                    child.MaterialId,
                    childVersionId!.Value,
                    grossQuantity,
                    path,
                    requirements);
                if (error is not null)
                {
                    return error;
                }
            }
            else
            {
                requirements.TryGetValue(child.MaterialId, out decimal existing);
                requirements[child.MaterialId] = existing + grossQuantity;
            }
        }

        path.Remove(materialId);
        return null;
    }

    private OracleConnection OpenConnection()
    {
        OracleConnection connection = new(connString);
        connection.Open();
        return connection;
    }

    private static bool BomVersionMatches(
        OracleConnection connection,
        long versionId,
        long materialId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM BOM_VERSION
              WHERE VERSION_ID = :versionId
                AND MATERIAL_ID = :materialId");
        command.Parameters.Add("versionId", OracleDbType.Int64).Value = versionId;
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static List<BomComponent> ReadBomComponents(
        OracleConnection connection,
        long materialId,
        long versionId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT CHILD_MATERIAL_ID, QUANTITY, LOSS_RATE
              FROM BOM
              WHERE PARENT_MATERIAL_ID = :materialId
                AND VERSION_ID = :versionId
              ORDER BY BOM_ID");
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        command.Parameters.Add("versionId", OracleDbType.Int64).Value = versionId;

        List<BomComponent> result = [];
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BomComponent(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToDecimal(reader.GetValue(1)),
                Convert.ToDecimal(reader.GetValue(2))));
        }

        return result;
    }

    private static long? ReadCurrentVersionId(OracleConnection connection, long materialId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT CURRENT_VERSION_ID
              FROM MATERIAL
              WHERE MATERIAL_ID = :materialId");
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static bool HasBomComponents(
        OracleConnection connection,
        long materialId,
        long versionId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM BOM
              WHERE PARENT_MATERIAL_ID = :materialId
                AND VERSION_ID = :versionId");
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        command.Parameters.Add("versionId", OracleDbType.Int64).Value = versionId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static decimal ReadAvailableQuantity(OracleConnection connection, long materialId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT AVAILABLE_QTY
              FROM MATERIAL_STOCK
              WHERE MATERIAL_ID = :materialId");
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
    }

    private static List<IncomingMaterial> ReadIncomingMaterials(
        OracleConnection connection,
        long materialId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT poi.QUANTITY - poi.RECEIVED_QTY, po.EXPECTED_DATE
              FROM PURCHASE_ORDER_ITEM poi
              JOIN PURCHASE_ORDER po ON po.ORDER_ID = poi.ORDER_ID
              WHERE poi.MATERIAL_ID = :materialId
                AND po.STATUS IN ('已提交', '部分到货')
                AND poi.QUANTITY > poi.RECEIVED_QTY
              ORDER BY po.EXPECTED_DATE, poi.ITEM_ID");
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;

        List<IncomingMaterial> result = [];
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new IncomingMaterial(
                Convert.ToDecimal(reader.GetValue(0)),
                DateOnly.FromDateTime(reader.GetDateTime(1))));
        }

        return result;
    }

    private sealed record BomComponent(long MaterialId, decimal Quantity, decimal LossRate);

    private sealed record IncomingMaterial(decimal Quantity, DateOnly ExpectedDate);
}
