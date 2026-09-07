using System.Data;

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
/// 生产订单审核阶段的物料锁定服务。物料需求只展开订单 BOM 的直接子项，
/// 预览与正式审核共用同一套计算口径。
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
            OrderContext? order = LoadOrder(connection, null, orderId, false);
            if (order is null)
            {
                return ProductionOrderMaterialLockPreviewResult.Fail(404, "生产订单不存在");
            }

            if (order.Status != ProductionStatusMap.Db.PendingReview)
            {
                return ProductionOrderMaterialLockPreviewResult.Fail(409, "仅待审核订单可预览物料锁定");
            }

            return ProductionOrderMaterialLockPreviewResult.Success(
                Evaluate(connection, null, order, false));
        }
        catch (MaterialLockException ex)
        {
            return ProductionOrderMaterialLockPreviewResult.Fail(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return ProductionOrderMaterialLockPreviewResult.Fail(500, $"物料锁定预览失败: {ex.Message}");
        }
    }

    public ProductionOrderResult Approve(ProductionOrderApproveRequest request, long operatorId)
    {
        using var connection = new OracleConnection(connString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            OrderContext? order = LoadOrder(connection, transaction, request.OrderId, true);
            if (order is null)
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(404, "生产订单不存在");
            }

            if (order.Status != ProductionStatusMap.Db.PendingReview)
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(409, "仅待审核订单可审核");
            }

            if (request.Approved)
            {
                ProductionOrderMaterialLockPreview preview =
                    Evaluate(connection, transaction, order, true);
                if (!preview.CanApprove)
                {
                    transaction.Rollback();
                    return ProductionOrderResult.Fail(409, BuildShortageMessage(preview.Items));
                }

                foreach (ProductionOrderMaterialLockItem item in preview.Items
                    .Where(item => item.PendingLockQty > 0)
                    .OrderBy(item => item.MaterialId))
                {
                    AddLock(connection, transaction, request.OrderId, operatorId, item);
                }
            }

            string nextStatus = request.Approved
                ? ProductionStatusMap.Db.PendingSchedule
                : ProductionStatusMap.Db.Cancelled;
            using (var command = OracleCommandFactory.Create(connection,
                @"UPDATE PRODUCTION_ORDER
                  SET STATUS = :status
                  WHERE ORDER_ID = :orderId AND STATUS = :expected", transaction))
            {
                command.Parameters.Add("status", OracleDbType.Varchar2).Value = nextStatus;
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.Parameters.Add("expected", OracleDbType.Varchar2).Value =
                    ProductionStatusMap.Db.PendingReview;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new MaterialLockException(409, "订单状态已变更，请刷新后重试");
                }
            }

            ProductionOrderDetail updated =
                ProductionOrderService.GetInternal(connection, request.OrderId, transaction)
                ?? throw new MaterialLockException(404, "生产订单不存在");
            transaction.Commit();
            return ProductionOrderResult.Success(updated);
        }
        catch (MaterialLockException ex)
        {
            transaction.Rollback();
            return ProductionOrderResult.Fail(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return ProductionOrderResult.Fail(500, $"审核生产订单失败: {ex.Message}");
        }
    }

    public ProductionOrderResult Start(ProductionOrderActionRequest request)
    {
        using var connection = new OracleConnection(connString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            OrderContext? order = LoadOrder(connection, transaction, request.OrderId, true);
            if (order is null)
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(404, "生产订单不存在");
            }

            if (order.Status != ProductionStatusMap.Db.PendingSchedule)
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(409, "仅待排产订单可开工");
            }

            ProductionOrderMaterialLockPreview preview =
                Evaluate(connection, transaction, order, true);
            if (preview.Items.Any(item => item.PendingLockQty > 0))
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(409, "订单物料锁定不完整，无法开工");
            }

            using (var command = OracleCommandFactory.Create(connection,
                @"UPDATE PRODUCTION_ORDER
                  SET STATUS = :status,
                      ACTUAL_START = TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                  WHERE ORDER_ID = :orderId AND STATUS = :expected", transaction))
            {
                command.Parameters.Add("status", OracleDbType.Varchar2).Value =
                    ProductionStatusMap.Db.InProgress;
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.Parameters.Add("expected", OracleDbType.Varchar2).Value =
                    ProductionStatusMap.Db.PendingSchedule;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new MaterialLockException(409, "订单状态已变更，请刷新后重试");
                }
            }

            ProductionOrderDetail updated =
                ProductionOrderService.GetInternal(connection, request.OrderId, transaction)
                ?? throw new MaterialLockException(404, "生产订单不存在");
            transaction.Commit();
            return ProductionOrderResult.Success(updated);
        }
        catch (MaterialLockException ex)
        {
            transaction.Rollback();
            return ProductionOrderResult.Fail(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return ProductionOrderResult.Fail(500, $"开始生产订单失败: {ex.Message}");
        }
    }

    public ProductionOrderResult Cancel(ProductionOrderActionRequest request)
    {
        using var connection = new OracleConnection(connString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            OrderContext? order = LoadOrder(connection, transaction, request.OrderId, true);
            if (order is null)
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(404, "生产订单不存在");
            }

            if (order.Status is ProductionStatusMap.Db.Completed or ProductionStatusMap.Db.Cancelled)
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(409, "已完工或已取消订单不可取消");
            }

            if (order.Status == ProductionStatusMap.Db.InProgress
                && HasCompletionReports(connection, transaction, request.OrderId))
            {
                transaction.Rollback();
                return ProductionOrderResult.Fail(409, "已发生完工报工的生产订单不可取消");
            }

            Dictionary<long, decimal> locks =
                LoadActiveLocks(connection, transaction, request.OrderId, true);
            foreach ((long materialId, decimal quantity) in locks.OrderBy(item => item.Key))
            {
                using var command = OracleCommandFactory.Create(connection,
                    @"UPDATE MATERIAL_STOCK
                      SET AVAILABLE_QTY = AVAILABLE_QTY + :quantity,
                          LOCKED_QTY = LOCKED_QTY - :quantity
                      WHERE MATERIAL_ID = :materialId AND LOCKED_QTY >= :quantity", transaction);
                command.Parameters.Add("quantity", OracleDbType.Decimal).Value = quantity;
                command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new MaterialLockException(409, $"物料 {materialId} 的锁定库存数据不一致");
                }
            }

            using (var command = OracleCommandFactory.Create(connection,
                @"UPDATE STOCK_LOCK
                  SET STATUS = :cancelled, RELEASE_TIME = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                  WHERE ORDER_ID = :orderId AND STATUS = :locked", transaction))
            {
                command.Parameters.Add("cancelled", OracleDbType.Varchar2).Value =
                    StockLockStatusMap.Db.Cancelled;
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.Parameters.Add("locked", OracleDbType.Varchar2).Value =
                    StockLockStatusMap.Db.Locked;
                command.ExecuteNonQuery();
            }

            using (var command = OracleCommandFactory.Create(connection,
                @"UPDATE PRODUCTION_ORDER SET STATUS = :status
                  WHERE ORDER_ID = :orderId
                    AND STATUS NOT IN (:completed, :cancelled)", transaction))
            {
                command.Parameters.Add("status", OracleDbType.Varchar2).Value =
                    ProductionStatusMap.Db.Cancelled;
                command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
                command.Parameters.Add("completed", OracleDbType.Varchar2).Value =
                    ProductionStatusMap.Db.Completed;
                command.Parameters.Add("cancelled", OracleDbType.Varchar2).Value =
                    ProductionStatusMap.Db.Cancelled;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new MaterialLockException(409, "订单状态已变更，请刷新后重试");
                }
            }

            ProductionOrderDetail updated =
                ProductionOrderService.GetInternal(connection, request.OrderId, transaction)
                ?? throw new MaterialLockException(404, "生产订单不存在");
            transaction.Commit();
            return ProductionOrderResult.Success(updated);
        }
        catch (MaterialLockException ex)
        {
            transaction.Rollback();
            return ProductionOrderResult.Fail(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return ProductionOrderResult.Fail(500, $"取消生产订单失败: {ex.Message}");
        }
    }

    private static ProductionOrderMaterialLockPreview Evaluate(
        OracleConnection connection,
        OracleTransaction? transaction,
        OrderContext order,
        bool lockRows)
    {
        List<Requirement> requirements = LoadRequirements(connection, transaction, order);
        if (requirements.Count == 0)
        {
            throw new MaterialLockException(409, "生产订单所选 BOM 没有直接子项，无法锁定生产物料");
        }

        Dictionary<long, decimal> activeLocks =
            LoadActiveLocks(connection, transaction, order.OrderId, lockRows);
        var requiredIds = requirements.Select(item => item.MaterialId).ToHashSet();
        long unrelatedLock = activeLocks.Keys.FirstOrDefault(materialId => !requiredIds.Contains(materialId));
        if (unrelatedLock != 0)
        {
            throw new MaterialLockException(409, $"订单存在非当前 BOM 物料 {unrelatedLock} 的有效锁定");
        }

        Dictionary<long, decimal> available =
            LoadAvailableStock(connection, transaction, requiredIds, lockRows);
        var items = new List<ProductionOrderMaterialLockItem>();
        foreach (Requirement requirement in requirements.OrderBy(item => item.MaterialId))
        {
            decimal lockedQty = activeLocks.GetValueOrDefault(requirement.MaterialId);
            if (lockedQty > requirement.RequiredQty)
            {
                throw new MaterialLockException(
                    409,
                    $"物料 {requirement.MaterialId} 的有效锁定数量超过订单需求");
            }

            decimal pendingQty = requirement.RequiredQty - lockedQty;
            decimal availableQty = available.GetValueOrDefault(requirement.MaterialId);
            items.Add(new ProductionOrderMaterialLockItem
            {
                MaterialId = requirement.MaterialId,
                MaterialName = requirement.MaterialName,
                Unit = requirement.Unit,
                BomQuantity = requirement.BomQuantity,
                LossRate = requirement.LossRate,
                RequiredQty = requirement.RequiredQty,
                LockedQty = lockedQty,
                PendingLockQty = pendingQty,
                AvailableQty = availableQty,
                ShortageQty = Math.Max(pendingQty - availableQty, 0),
            });
        }

        return new ProductionOrderMaterialLockPreview
        {
            OrderId = order.OrderId,
            CanApprove = items.All(item => item.ShortageQty == 0),
            Items = items,
        };
    }

    private static void AddLock(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId,
        long operatorId,
        ProductionOrderMaterialLockItem item)
    {
        using (var command = OracleCommandFactory.Create(connection,
            @"UPDATE MATERIAL_STOCK
              SET AVAILABLE_QTY = AVAILABLE_QTY - :quantity,
                  LOCKED_QTY = LOCKED_QTY + :quantity,
                  LAST_OUT_DATE = SYS_EXTRACT_UTC(SYSTIMESTAMP)
              WHERE MATERIAL_ID = :materialId AND AVAILABLE_QTY >= :quantity", transaction))
        {
            command.Parameters.Add("quantity", OracleDbType.Decimal).Value = item.PendingLockQty;
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = item.MaterialId;
            if (command.ExecuteNonQuery() != 1)
            {
                throw new MaterialLockException(409, $"物料 {item.MaterialId} 库存不足，未执行审核");
            }
        }

        using (var command = OracleCommandFactory.Create(connection,
            @"INSERT INTO STOCK_LOCK
              (ORDER_ID, MATERIAL_ID, LOCK_QTY, STATUS, LOCK_TIME, OPERATOR_ID)
              VALUES (:orderId, :materialId, :quantity, :status,
                      SYS_EXTRACT_UTC(SYSTIMESTAMP), :operatorId)", transaction))
        {
            command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = item.MaterialId;
            command.Parameters.Add("quantity", OracleDbType.Decimal).Value = item.PendingLockQty;
            command.Parameters.Add("status", OracleDbType.Varchar2).Value =
                StockLockStatusMap.Db.Locked;
            command.Parameters.Add("operatorId", OracleDbType.Int64).Value = operatorId;
            command.ExecuteNonQuery();
        }
    }

    private static OrderContext? LoadOrder(
        OracleConnection connection,
        OracleTransaction? transaction,
        long orderId,
        bool forUpdate)
    {
        string suffix = forUpdate ? " FOR UPDATE" : string.Empty;
        using var command = OracleCommandFactory.Create(connection,
            @"SELECT ORDER_ID, MATERIAL_ID, VERSION_ID, PLAN_QTY, STATUS
              FROM PRODUCTION_ORDER WHERE ORDER_ID = :orderId" + suffix, transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new OrderContext(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToInt64(reader.GetValue(1)),
                Convert.ToInt64(reader.GetValue(2)),
                reader.GetDecimal(3),
                reader.GetString(4))
            : null;
    }

    private static List<Requirement> LoadRequirements(
        OracleConnection connection,
        OracleTransaction? transaction,
        OrderContext order)
    {
        using var command = OracleCommandFactory.Create(connection,
            @"SELECT b.CHILD_MATERIAL_ID, m.MATERIAL_NAME, m.UNIT,
                     b.QUANTITY, b.LOSS_RATE
              FROM BOM b
              JOIN MATERIAL m ON m.MATERIAL_ID = b.CHILD_MATERIAL_ID
              WHERE b.VERSION_ID = :versionId
                AND b.PARENT_MATERIAL_ID = :materialId
              ORDER BY b.CHILD_MATERIAL_ID", transaction);
        command.Parameters.Add("versionId", OracleDbType.Int64).Value = order.VersionId;
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = order.MaterialId;

        var result = new List<Requirement>();
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            decimal bomQuantity = reader.GetDecimal(3);
            decimal lossRate = reader.GetDecimal(4);
            if (bomQuantity <= 0 || lossRate < 0 || lossRate >= 1)
            {
                throw new MaterialLockException(409, "BOM 用量或损耗率无效");
            }

            decimal requiredQty =
                decimal.Ceiling(order.PlanQty * bomQuantity / (1 - lossRate) * 100) / 100;
            result.Add(new Requirement(
                Convert.ToInt64(reader.GetValue(0)),
                reader.GetString(1),
                reader.GetString(2),
                bomQuantity,
                lossRate,
                requiredQty));
        }

        return result;
    }

    private static Dictionary<long, decimal> LoadActiveLocks(
        OracleConnection connection,
        OracleTransaction? transaction,
        long orderId,
        bool forUpdate)
    {
        if (forUpdate)
        {
            using var lockCommand = OracleCommandFactory.Create(connection,
                @"SELECT LOCK_ID FROM STOCK_LOCK
                  WHERE ORDER_ID = :orderId AND STATUS = :status
                  ORDER BY LOCK_ID FOR UPDATE", transaction);
            lockCommand.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
            lockCommand.Parameters.Add("status", OracleDbType.Varchar2).Value =
                StockLockStatusMap.Db.Locked;
            using OracleDataReader lockReader = lockCommand.ExecuteReader();
            while (lockReader.Read())
            {
            }
        }

        using var command = OracleCommandFactory.Create(connection,
            @"SELECT MATERIAL_ID, SUM(LOCK_QTY)
              FROM STOCK_LOCK
              WHERE ORDER_ID = :orderId AND STATUS = :status
              GROUP BY MATERIAL_ID", transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        command.Parameters.Add("status", OracleDbType.Varchar2).Value =
            StockLockStatusMap.Db.Locked;
        var result = new Dictionary<long, decimal>();
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Convert.ToInt64(reader.GetValue(0)), reader.GetDecimal(1));
        }

        return result;
    }

    private static Dictionary<long, decimal> LoadAvailableStock(
        OracleConnection connection,
        OracleTransaction? transaction,
        IEnumerable<long> materialIds,
        bool forUpdate)
    {
        var result = new Dictionary<long, decimal>();
        foreach (long materialId in materialIds.Order())
        {
            string suffix = forUpdate ? " FOR UPDATE" : string.Empty;
            using var command = OracleCommandFactory.Create(connection,
                @"SELECT AVAILABLE_QTY FROM MATERIAL_STOCK
                  WHERE MATERIAL_ID = :materialId" + suffix, transaction);
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
            object? value = command.ExecuteScalar();
            result[materialId] = value is null or DBNull ? 0 : Convert.ToDecimal(value);
        }

        return result;
    }

    private static bool HasCompletionReports(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId)
    {
        using var command = OracleCommandFactory.Create(connection,
            "SELECT COUNT(*) FROM FINISH_INBOUND WHERE ORDER_ID = :orderId", transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static string BuildShortageMessage(IEnumerable<ProductionOrderMaterialLockItem> items)
    {
        string detail = string.Join("；", items
            .Where(item => item.ShortageQty > 0)
            .Select(item => $"{item.MaterialName}缺少{item.ShortageQty:0.##}{item.Unit}"));
        return string.IsNullOrEmpty(detail) ? "库存不足，未执行审核" : $"库存不足：{detail}";
    }

    private sealed record OrderContext(
        long OrderId,
        long MaterialId,
        long VersionId,
        decimal PlanQty,
        string Status);

    private sealed record Requirement(
        long MaterialId,
        string MaterialName,
        string Unit,
        decimal BomQuantity,
        decimal LossRate,
        decimal RequiredQty);

    private sealed class MaterialLockException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }
}
