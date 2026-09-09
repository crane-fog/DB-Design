using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed record ProductionCompletionReportOutcome(
    bool Ok,
    ProductionCompletionReportResult? Data,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ProductionCompletionReportOutcome Success(ProductionCompletionReportResult data) =>
        new(true, data, 200, null);

    public static ProductionCompletionReportOutcome Fail(int code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// Calls PKG_PRODUCTION_DOMAIN for atomic completion, consumption-snapshot allocation,
/// finished-stock movement and final lock settlement. The API layer owns the transaction.
/// </summary>
public class ProductionCompletionService(
    string connString,
    ILogger<ProductionCompletionService> logger)
{
    public ProductionCompletionReportOutcome Report(
        ProductionCompletionReportRequest request,
        CurrentUser currentUser)
    {
        string batchNo = request.BatchNo?.Trim() ?? string.Empty;
        if (request.OrderId <= 0)
        {
            return ProductionCompletionReportOutcome.Fail(400, "生产订单编号无效");
        }

        if (request.FinishQty <= 0)
        {
            return ProductionCompletionReportOutcome.Fail(400, "本批完工数量必须大于 0");
        }

        if (request.QualifiedQty < 0 || request.QualifiedQty > request.FinishQty)
        {
            return ProductionCompletionReportOutcome.Fail(400, "本批合格数量必须介于 0 和完工数量之间");
        }

        if (batchNo.Length is < 1 or > 30)
        {
            return ProductionCompletionReportOutcome.Fail(400, "批次号长度必须为 1 到 30 个字符");
        }

        using var connection = new OracleConnection(connString);
        connection.Open();
        using OracleTransaction transaction = connection.BeginTransaction();
        try
        {
            using var command = OracleCommandFactory.Create(
                connection,
                @"BEGIN
                    PKG_PRODUCTION_DOMAIN.REPORT_COMPLETION(
                        :orderId, :finishQty, :qualifiedQty, :batchNo, :operatorId,
                        :inboundId, :orderCompleted);
                  END;",
                transaction);
            command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
            command.Parameters.Add("finishQty", OracleDbType.Decimal).Value = request.FinishQty;
            command.Parameters.Add("qualifiedQty", OracleDbType.Decimal).Value = request.QualifiedQty;
            command.Parameters.Add("batchNo", OracleDbType.Varchar2).Value = batchNo;
            command.Parameters.Add("operatorId", OracleDbType.Int64).Value = currentUser.UserId;
            var inboundParameter = command.Parameters.Add("inboundId", OracleDbType.Int64);
            inboundParameter.Direction = System.Data.ParameterDirection.Output;
            var completedParameter = command.Parameters.Add("orderCompleted", OracleDbType.Int32);
            completedParameter.Direction = System.Data.ParameterDirection.Output;
            command.ExecuteNonQuery();

            long inboundId = OracleCommandFactory.ReadIdentity(inboundParameter);
            bool orderCompleted = Convert.ToInt32(completedParameter.Value.ToString()) == 1;
            List<StockLockRecord> consumedLocks = orderCompleted
                ? GetConsumedLocks(connection, transaction, request.OrderId)
                : [];
            ProductionOrderDetail productionOrder = ProductionOrderService.GetInternal(
                connection,
                request.OrderId,
                transaction)
                ?? throw new InvalidOperationException("报工后无法读取生产订单");
            CompletionInboundOrder completionInbound = GetInbound(
                connection,
                transaction,
                inboundId,
                consumedLocks)
                ?? throw new InvalidOperationException("报工后无法读取完工入库记录");

            transaction.Commit();
            return ProductionCompletionReportOutcome.Success(new ProductionCompletionReportResult
            {
                ProductionOrder = productionOrder,
                CompletionInbound = completionInbound,
                OrderCompleted = orderCompleted,
            });
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            RollbackQuietly(transaction);
            return ProductionCompletionReportOutcome.Fail(code, message);
        }
        catch (Exception exception)
        {
            RollbackQuietly(transaction);
            logger.LogError(exception, "生产订单 {OrderId} 完工报工失败", request.OrderId);
            return ProductionCompletionReportOutcome.Fail(500, "完工报工失败");
        }
    }

    private static List<StockLockRecord> GetConsumedLocks(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId)
    {
        var records = new List<StockLockRecord>();
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT l.LOCK_ID, l.ORDER_ID, l.MATERIAL_ID, m.MATERIAL_NAME,
                     l.LOCK_QTY, l.LOCK_TIME, l.RELEASE_TIME, l.STATUS, l.OPERATOR_ID
              FROM STOCK_LOCK l
              LEFT JOIN MATERIAL m ON m.MATERIAL_ID = l.MATERIAL_ID
              WHERE l.ORDER_ID = :orderId AND l.STATUS = :status
              ORDER BY l.LOCK_ID",
            transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        command.Parameters.Add("status", OracleDbType.Varchar2).Value =
            StockLockStatusMap.Db.Consumed;
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new StockLockRecord
            {
                LockId = Convert.ToInt64(reader.GetValue(0)),
                OrderId = Convert.ToInt64(reader.GetValue(1)),
                MaterialId = Convert.ToInt64(reader.GetValue(2)),
                MaterialName = reader.IsDBNull(3) ? null! : reader.GetString(3),
                LockQty = reader.GetDecimal(4),
                LockTime = reader.GetUtcDateTime(5),
                ReleaseTime = reader.IsDBNull(6) ? null : reader.GetUtcDateTime(6),
                Status = StockLockStatusMap.FromDb(reader.GetString(7)),
                OperatorId = Convert.ToInt64(reader.GetValue(8)),
            });
        }

        return records;
    }

    private static CompletionInboundOrder? GetInbound(
        OracleConnection connection,
        OracleTransaction transaction,
        long inboundId,
        List<StockLockRecord> consumedLocks)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT i.INBOUND_ID, i.ORDER_ID, i.MATERIAL_ID, m.MATERIAL_NAME,
                     i.VERSION_ID, i.FINISH_QTY, i.QUALIFIED_QTY, i.BATCH_NO,
                     i.INBOUND_TIME, i.OPERATOR_ID, u.USER_NAME
              FROM FINISH_INBOUND i
              LEFT JOIN MATERIAL m ON m.MATERIAL_ID = i.MATERIAL_ID
              LEFT JOIN SYS_USER u ON u.USER_ID = i.OPERATOR_ID
              WHERE i.INBOUND_ID = :inboundId",
            transaction);
        command.Parameters.Add("inboundId", OracleDbType.Int64).Value = inboundId;
        using OracleDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new CompletionInboundOrder
        {
            InboundId = Convert.ToInt64(reader.GetValue(0)),
            OrderId = Convert.ToInt64(reader.GetValue(1)),
            MaterialId = Convert.ToInt64(reader.GetValue(2)),
            ProductName = reader.IsDBNull(3) ? null! : reader.GetString(3),
            VersionId = Convert.ToInt64(reader.GetValue(4)),
            FinishQty = reader.GetDecimal(5),
            QualifiedQty = reader.GetDecimal(6),
            BatchNo = reader.GetString(7),
            InboundTime = reader.GetUtcDateTime(8),
            OperatorId = Convert.ToInt64(reader.GetValue(9)),
            OperatorName = reader.IsDBNull(10) ? null! : reader.GetString(10),
            ConsumedLockRecords = consumedLocks,
        };
    }

    private static void RollbackQuietly(OracleTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (InvalidOperationException)
        {
            // The transaction has already ended.
        }
        catch (OracleException)
        {
            // Preserve the original error; disposing the connection is the final fallback.
        }
    }
}
