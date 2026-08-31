using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed class CapacityEstimationDataSource(
    string connString, MaterialRequirementNettingService requirementNetting)
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
                    DateOnly.FromDateTime(reader.GetDateTime(3)),
                    request.OrderId);
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
            // Keep capacity's existing safety-stock and current-version policies, while sharing
            // inventory allocation with demand planning. Temporary estimates have no order locks.
            var plan = requirementNetting.Calculate(connection, null,
                [new ProductionRequirement(input.MaterialId, input.VersionId, input.PlanQty)],
                new MaterialPlanningOptions(
                    IncludeSafetyStock: false, OrderId: input.OrderId, RequireEffectiveChildVersions: false));
            return ProductionResourceResult<MaterialReadiness>.Success(plan.GetMaterialReadiness());
        }
        catch (MaterialPlanningException ex)
        {
            return ProductionResourceResult<MaterialReadiness>.Fail(ex.Code, ex.Message);
        }
        catch (OracleException)
        {
            return ProductionResourceResult<MaterialReadiness>.Fail(
                500,
                "计算物料齐套情况失败");
        }
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

}
