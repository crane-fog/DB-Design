using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed record ProductionOrderMaterialLockPreviewResult(
    bool Ok,
    ProductionOrderMaterialLockPreview? Data,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ProductionOrderMaterialLockPreviewResult Success(
        ProductionOrderMaterialLockPreview data) => new(true, data, 200, null);

    public static ProductionOrderMaterialLockPreviewResult Fail(int code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// Thin application adapter over PKG_PRODUCTION_DOMAIN. Oracle owns lock calculation,
/// concurrency checks, stock movement and order transitions; this class owns commit/rollback.
/// </summary>
public sealed class ProductionOrderMaterialLockService(string connString)
{
    public ProductionOrderMaterialLockPreviewResult Preview(long orderId)
    {
        if (orderId <= 0)
        {
            return ProductionOrderMaterialLockPreviewResult.Fail(400, "生产订单编号必须大于 0");
        }

        using var connection = new OracleConnection(connString);
        connection.Open();
        try
        {
            using var command = OracleCommandFactory.Create(
                connection,
                "BEGIN PKG_PRODUCTION_DOMAIN.OPEN_LOCK_PREVIEW(:orderId, :rows); END;");
            command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
            command.Parameters.Add("rows", OracleDbType.RefCursor).Direction =
                System.Data.ParameterDirection.Output;

            var items = new List<ProductionOrderMaterialLockItem>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new ProductionOrderMaterialLockItem
                {
                    MaterialId = Convert.ToInt64(reader.GetValue(0)),
                    MaterialName = reader.GetString(1),
                    Unit = reader.GetString(2),
                    BomQuantity = reader.GetDecimal(3),
                    LossRate = reader.GetDecimal(4),
                    RequiredQty = reader.GetDecimal(5),
                    LockedQty = reader.GetDecimal(6),
                    PendingLockQty = reader.GetDecimal(7),
                    AvailableQty = reader.GetDecimal(8),
                    ShortageQty = reader.GetDecimal(9),
                });
            }

            return ProductionOrderMaterialLockPreviewResult.Success(
                new ProductionOrderMaterialLockPreview
                {
                    OrderId = orderId,
                    CanApprove = items.All(item => item.ShortageQty == 0),
                    Items = items,
                });
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            return ProductionOrderMaterialLockPreviewResult.Fail(code, message);
        }
        catch (Exception exception)
        {
            return ProductionOrderMaterialLockPreviewResult.Fail(
                500,
                $"物料锁定预览失败: {exception.Message}");
        }
    }

    public ProductionOrderResult Approve(ProductionOrderApproveRequest request, long operatorId) =>
        ExecuteOrderAction(
            request.OrderId,
            "审核生产订单失败",
            (connection, transaction) =>
            {
                using var command = OracleCommandFactory.Create(
                    connection,
                    "BEGIN PKG_PRODUCTION_DOMAIN.APPROVE_ORDER(:orderId, :approved, :operatorId); END;",
                    transaction);
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.Parameters.Add("approved", OracleDbType.Int32).Value = request.Approved ? 1 : 0;
                command.Parameters.Add("operatorId", OracleDbType.Int64).Value = operatorId;
                command.ExecuteNonQuery();
            });

    public ProductionOrderResult Start(ProductionOrderActionRequest request) =>
        ExecuteOrderAction(
            request.OrderId,
            "开始生产订单失败",
            (connection, transaction) =>
            {
                using var command = OracleCommandFactory.Create(
                    connection,
                    "BEGIN PKG_PRODUCTION_DOMAIN.START_ORDER(:orderId); END;",
                    transaction);
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.ExecuteNonQuery();
            });

    public ProductionOrderResult Cancel(ProductionOrderActionRequest request) =>
        ExecuteOrderAction(
            request.OrderId,
            "取消生产订单失败",
            (connection, transaction) =>
            {
                using var command = OracleCommandFactory.Create(
                    connection,
                    "BEGIN PKG_PRODUCTION_DOMAIN.CANCEL_ORDER(:orderId); END;",
                    transaction);
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.ExecuteNonQuery();
            });

    private ProductionOrderResult ExecuteOrderAction(
        long orderId,
        string failureMessage,
        Action<OracleConnection, OracleTransaction> action)
    {
        using var connection = new OracleConnection(connString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            action(connection, transaction);
            ProductionOrderDetail updated = ProductionOrderService.GetInternal(
                connection,
                orderId,
                transaction)
                ?? throw new InvalidOperationException("数据库操作后无法读取生产订单");
            transaction.Commit();
            return ProductionOrderResult.Success(updated);
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            RollbackQuietly(transaction);
            return ProductionOrderResult.Fail(code, message);
        }
        catch (Exception exception)
        {
            RollbackQuietly(transaction);
            return ProductionOrderResult.Fail(500, $"{failureMessage}: {exception.Message}");
        }
    }

    private static void RollbackQuietly(OracleTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (OracleException)
        {
            // Preserve the original domain error returned to the API caller.
        }
    }
}
