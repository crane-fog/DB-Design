using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>生产订单分批完工报工结果。</summary>
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
/// 在一个事务内登记完工批次、增加合格品库存、累计订单合格数量，并在达标时结清原料锁定。
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
            ProductionOrderReportContext? order = GetOrderForUpdate(
                connection,
                transaction,
                request.OrderId);
            if (order is null)
            {
                transaction.Rollback();
                return ProductionCompletionReportOutcome.Fail(404, "生产订单不存在");
            }

            if (order.Status != ProductionStatusMap.Db.InProgress)
            {
                transaction.Rollback();
                return ProductionCompletionReportOutcome.Fail(409, "仅生产中订单可进行完工报工");
            }

            if (BatchExists(connection, transaction, batchNo))
            {
                transaction.Rollback();
                return ProductionCompletionReportOutcome.Fail(409, "批次号已存在");
            }

            long inboundId = InsertInbound(
                connection,
                transaction,
                request,
                order,
                batchNo,
                currentUser.UserId);

            AddFinishedStock(
                connection,
                transaction,
                order.MaterialId,
                request.QualifiedQty);

            decimal cumulativeQualifiedQty = order.FinishedQty + request.QualifiedQty;
            bool orderCompleted = cumulativeQualifiedQty >= order.PlanQty;
            UpdateOrderProgress(
                connection,
                transaction,
                request.OrderId,
                cumulativeQualifiedQty,
                orderCompleted);

            List<StockLockRecord> consumedLocks = orderCompleted
                ? ConsumeOrderLocks(connection, transaction, request.OrderId)
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
        catch (OracleException exception) when (exception.Number == 1)
        {
            RollbackQuietly(transaction);
            return ProductionCompletionReportOutcome.Fail(409, "批次号已存在");
        }
        catch (ProductionCompletionConflictException exception)
        {
            RollbackQuietly(transaction);
            return ProductionCompletionReportOutcome.Fail(409, exception.Message);
        }
        catch (Exception exception)
        {
            RollbackQuietly(transaction);
            logger.LogError(exception, "生产订单 {OrderId} 完工报工失败", request.OrderId);
            return ProductionCompletionReportOutcome.Fail(500, "完工报工失败");
        }
    }

    private static ProductionOrderReportContext? GetOrderForUpdate(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT MATERIAL_ID, VERSION_ID, PLAN_QTY, FINISHED_QTY, STATUS
              FROM PRODUCTION_ORDER
              WHERE ORDER_ID = :orderId
              FOR UPDATE",
            transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;

        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new ProductionOrderReportContext(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToInt64(reader.GetValue(1)),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetString(4))
            : null;
    }

    private static bool BatchExists(
        OracleConnection connection,
        OracleTransaction transaction,
        string batchNo)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM FINISH_INBOUND WHERE BATCH_NO = :batchNo",
            transaction);
        command.Parameters.Add("batchNo", OracleDbType.Varchar2).Value = batchNo;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static long InsertInbound(
        OracleConnection connection,
        OracleTransaction transaction,
        ProductionCompletionReportRequest request,
        ProductionOrderReportContext order,
        string batchNo,
        long operatorId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"INSERT INTO FINISH_INBOUND
                (ORDER_ID, MATERIAL_ID, VERSION_ID, FINISH_QTY, QUALIFIED_QTY,
                 BATCH_NO, INBOUND_TIME, OPERATOR_ID)
              VALUES
                (:orderId, :materialId, :versionId, :finishQty, :qualifiedQty,
                 :batchNo, SYS_EXTRACT_UTC(SYSTIMESTAMP), :operatorId)
              RETURNING INBOUND_ID INTO :newId",
            transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = order.MaterialId;
        command.Parameters.Add("versionId", OracleDbType.Int64).Value = order.VersionId;
        command.Parameters.Add("finishQty", OracleDbType.Decimal).Value = request.FinishQty;
        command.Parameters.Add("qualifiedQty", OracleDbType.Decimal).Value = request.QualifiedQty;
        command.Parameters.Add("batchNo", OracleDbType.Varchar2).Value = batchNo;
        command.Parameters.Add("operatorId", OracleDbType.Int64).Value = operatorId;
        var identity = new OracleParameter("newId", OracleDbType.Int64)
        {
            Direction = System.Data.ParameterDirection.Output,
        };
        command.Parameters.Add(identity);
        command.ExecuteNonQuery();
        return OracleCommandFactory.ReadIdentity(identity);
    }

    private static void AddFinishedStock(
        OracleConnection connection,
        OracleTransaction transaction,
        long materialId,
        decimal qualifiedQty)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"MERGE INTO MATERIAL_STOCK target
              USING (SELECT :materialId AS MATERIAL_ID FROM DUAL) source
              ON (target.MATERIAL_ID = source.MATERIAL_ID)
              WHEN MATCHED THEN UPDATE SET
                  target.AVAILABLE_QTY = target.AVAILABLE_QTY + :qualifiedQty,
                  target.LAST_IN_DATE = SYS_EXTRACT_UTC(SYSTIMESTAMP)
              WHEN NOT MATCHED THEN INSERT
                  (MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY, LAST_IN_DATE)
              VALUES
                  (:insertMaterialId, :insertQualifiedQty, 0, SYS_EXTRACT_UTC(SYSTIMESTAMP))",
            transaction);
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        command.Parameters.Add("qualifiedQty", OracleDbType.Decimal).Value = qualifiedQty;
        command.Parameters.Add("insertMaterialId", OracleDbType.Int64).Value = materialId;
        command.Parameters.Add("insertQualifiedQty", OracleDbType.Decimal).Value = qualifiedQty;
        command.ExecuteNonQuery();
    }

    private static void UpdateOrderProgress(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId,
        decimal cumulativeQualifiedQty,
        bool completed)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"UPDATE PRODUCTION_ORDER
              SET FINISHED_QTY = :finishedQty,
                  STATUS = :status,
                  ACTUAL_END = CASE
                      WHEN :isCompleted = 1
                      THEN TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                      ELSE ACTUAL_END
                  END
              WHERE ORDER_ID = :orderId AND STATUS = :expectedStatus",
            transaction);
        command.Parameters.Add("finishedQty", OracleDbType.Decimal).Value = cumulativeQualifiedQty;
        command.Parameters.Add("status", OracleDbType.Varchar2).Value = completed
            ? ProductionStatusMap.Db.Completed
            : ProductionStatusMap.Db.InProgress;
        command.Parameters.Add("isCompleted", OracleDbType.Int32).Value = completed ? 1 : 0;
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        command.Parameters.Add("expectedStatus", OracleDbType.Varchar2).Value =
            ProductionStatusMap.Db.InProgress;

        if (command.ExecuteNonQuery() != 1)
        {
            throw new ProductionCompletionConflictException("生产订单状态已变化，请刷新后重试");
        }
    }

    private static List<StockLockRecord> ConsumeOrderLocks(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId)
    {
        var lockedQuantities = new Dictionary<long, decimal>();
        using (OracleCommand selectCommand = OracleCommandFactory.Create(
                   connection,
                   @"SELECT MATERIAL_ID, SUM(LOCK_QTY)
                     FROM STOCK_LOCK
                     WHERE ORDER_ID = :orderId AND STATUS = :status
                     GROUP BY MATERIAL_ID",
                   transaction))
        {
            selectCommand.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
            selectCommand.Parameters.Add("status", OracleDbType.Varchar2).Value =
                StockLockStatusMap.Db.Locked;
            using OracleDataReader reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                lockedQuantities[Convert.ToInt64(reader.GetValue(0))] = reader.GetDecimal(1);
            }
        }

        using (OracleCommand updateLocksCommand = OracleCommandFactory.Create(
                   connection,
                   @"UPDATE STOCK_LOCK
                     SET STATUS = :newStatus,
                         RELEASE_TIME = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                     WHERE ORDER_ID = :orderId AND STATUS = :expectedStatus",
                   transaction))
        {
            updateLocksCommand.Parameters.Add("newStatus", OracleDbType.Varchar2).Value =
                StockLockStatusMap.Db.Consumed;
            updateLocksCommand.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
            updateLocksCommand.Parameters.Add("expectedStatus", OracleDbType.Varchar2).Value =
                StockLockStatusMap.Db.Locked;
            updateLocksCommand.ExecuteNonQuery();
        }

        foreach ((long materialId, decimal lockedQty) in lockedQuantities)
        {
            using OracleCommand stockCommand = OracleCommandFactory.Create(
                connection,
                @"UPDATE MATERIAL_STOCK
                  SET LOCKED_QTY = LOCKED_QTY - :lockedQty
                  WHERE MATERIAL_ID = :materialId AND LOCKED_QTY >= :lockedQty",
                transaction);
            stockCommand.Parameters.Add("lockedQty", OracleDbType.Decimal).Value = lockedQty;
            stockCommand.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
            if (stockCommand.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException($"物料 {materialId} 的锁定库存数据不一致");
            }
        }

        return GetConsumedLocks(connection, transaction, orderId);
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
            // 事务已经结束时无需再次回滚。
        }
        catch (OracleException)
        {
            // 保留原始业务异常，回滚失败由连接释放兜底。
        }
    }

    private sealed record ProductionOrderReportContext(
        long MaterialId,
        long VersionId,
        decimal PlanQty,
        decimal FinishedQty,
        string Status);

    private sealed class ProductionCompletionConflictException(string message) : Exception(message);
}
