using System.Data;

using Backend.Domain;

using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed record PurchaseResult(
    bool Ok,
    PurchaseOrder? Order,
    int ErrorCode,
    string? ErrorMessage)
{
    public static PurchaseResult Success(PurchaseOrder order) =>
        new(true, order, 200, null);
    public static PurchaseResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record PurchaseReceiptResult(
    bool Ok,
    PurchaseReceipt? Receipt,
    int ErrorCode,
    string? ErrorMessage)
{
    public static PurchaseReceiptResult Success(PurchaseReceipt receipt) =>
        new(true, receipt, 200, null);
    public static PurchaseReceiptResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record ReminderResult(
    bool Ok,
    PurchaseOverdueReminder? Reminder,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ReminderResult Success(PurchaseOverdueReminder reminder) =>
        new(true, reminder, 200, null);
    public static ReminderResult Fail(int code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// 采购管理主责 Service（B 模块）。维护 purchase_order、purchase_order_item、
/// receive_record、overdue_reminder 表。
/// </summary>
public class PurchaseService(string connString)
{
    // ── purchase_order (master) ───────────────────────────────────
    private const string OrderColumns = @"
        SELECT o.ORDER_ID, o.STATUS, o.SUPPLIER_ID, s.SUPPLIER_NAME,
               s.CONTACT_PERSON, s.CONTACT_PHONE,
               o.ORDER_DATE, o.EXPECTED_DATE, o.ACTUAL_DATE,
               o.BUYER_ID, o.TOTAL_AMOUNT
        FROM PURCHASE_ORDER o
        JOIN SUPPLIER s ON s.SUPPLIER_ID = o.SUPPLIER_ID";

    // ── purchase_order_item ───────────────────────────────────────
    private const string ItemColumns = @"
        SELECT i.ITEM_ID, i.ORDER_ID, i.MATERIAL_ID, m.MATERIAL_NAME,
               i.QUANTITY, COALESCE(i.RECEIVED_QTY, 0), i.UNIT_PRICE
        FROM PURCHASE_ORDER_ITEM i
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = i.MATERIAL_ID";

    // ── receive_record ────────────────────────────────────────────
    private const string ReceiptColumns = @"
        SELECT r.RECEIVE_ID, r.ORDER_ID, r.MATERIAL_ID, m.MATERIAL_NAME,
               r.QUANTITY, r.RECEIVE_DATE
        FROM RECEIVE_RECORD r
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = r.MATERIAL_ID";

    // ── overdue_reminder ──────────────────────────────────────────
    private const string ReminderColumns = @"
        SELECT r.REMINDER_ID, r.ORDER_ID, r.EXPECTED_DATE, r.OVERDUE_DAYS,
               r.REMIND_TIME, r.STATUS, r.REMARK
        FROM OVERDUE_REMINDER r";

    // ═══════════════════════════════════════════════════════════════
    //  采购订单 列表 / 详情
    // ═══════════════════════════════════════════════════════════════

    public (List<PurchaseOrder> Records, int Total) List(
        int page, int pageSize,
        long? supplierId, long? materialId, string? dbStatus,
        DateOnly? orderDateStart, DateOnly? orderDateEnd,
        DateOnly? expectedDateStart, DateOnly? expectedDateEnd,
        long? buyerId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();

        if (supplierId.HasValue) where.Add("o.SUPPLIER_ID = :supplierId");
        if (!string.IsNullOrEmpty(dbStatus)) where.Add("o.STATUS = :status");
        if (orderDateStart.HasValue) where.Add("o.ORDER_DATE >= :orderDateStart");
        if (orderDateEnd.HasValue) where.Add("o.ORDER_DATE <= :orderDateEnd");
        if (expectedDateStart.HasValue) where.Add("o.EXPECTED_DATE >= :expectedDateStart");
        if (expectedDateEnd.HasValue) where.Add("o.EXPECTED_DATE <= :expectedDateEnd");
        if (buyerId.HasValue) where.Add("o.BUYER_ID = :buyerId");

        if (materialId.HasValue)
            where.Add(@"o.ORDER_ID IN (
                SELECT DISTINCT i.ORDER_ID FROM PURCHASE_ORDER_ITEM i WHERE i.MATERIAL_ID = :materialId)");

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (supplierId.HasValue) cmd.Parameters.Add(new OracleParameter("supplierId", supplierId.Value));
            if (!string.IsNullOrEmpty(dbStatus)) cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            if (orderDateStart.HasValue) cmd.Parameters.Add(new OracleParameter("orderDateStart", orderDateStart.Value.ToDateTime(TimeOnly.MinValue)));
            if (orderDateEnd.HasValue) cmd.Parameters.Add(new OracleParameter("orderDateEnd", orderDateEnd.Value.ToDateTime(TimeOnly.MinValue)));
            if (expectedDateStart.HasValue) cmd.Parameters.Add(new OracleParameter("expectedDateStart", expectedDateStart.Value.ToDateTime(TimeOnly.MinValue)));
            if (expectedDateEnd.HasValue) cmd.Parameters.Add(new OracleParameter("expectedDateEnd", expectedDateEnd.Value.ToDateTime(TimeOnly.MinValue)));
            if (buyerId.HasValue) cmd.Parameters.Add(new OracleParameter("buyerId", buyerId.Value));
            if (materialId.HasValue) cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM PURCHASE_ORDER o" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<PurchaseOrder>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = OrderColumns + whereClause +
                @" ORDER BY o.ORDER_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapOrder(reader, conn));
        }

        return (records, total);
    }

    public PurchaseOrder? Get(long orderId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        return GetOrderInternal(conn, orderId);
    }

    // ═══════════════════════════════════════════════════════════════
    //  采购订单 创建
    // ═══════════════════════════════════════════════════════════════

    public PurchaseResult Create(PurchaseOrderCreateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        if (request.Details is null || request.Details.Count == 0)
            return PurchaseResult.Fail(400, "采购明细不能为空");

        foreach (var d in request.Details)
        {
            if (d.Quantity <= 0)
                return PurchaseResult.Fail(400, $"物料 {d.MaterialId} 数量必须大于 0");
            if (!MaterialExists(conn, d.MaterialId))
                return PurchaseResult.Fail(400, $"物料 {d.MaterialId} 不存在");
        }

        if (!SupplierExists(conn, request.SupplierId))
            return PurchaseResult.Fail(400, "供应商不存在");

        decimal totalAmount = request.Details.Sum(d => d.Quantity * d.UnitPrice);

        long newOrderId;
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO PURCHASE_ORDER
                        (STATUS, SUPPLIER_ID, ORDER_DATE, EXPECTED_DATE, BUYER_ID, TOTAL_AMOUNT)
                        VALUES (:status, :supplierId, SYSDATE, :expectedDate, :buyerId, :totalAmount)
                        RETURNING ORDER_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("status", PurchaseOrderStatusMap.Db.Draft));
                    cmd.Parameters.Add(new OracleParameter("supplierId", request.SupplierId));
                    cmd.Parameters.Add(new OracleParameter("expectedDate", request.ExpectedDate.ToDateTime(TimeOnly.MinValue)));
                    cmd.Parameters.Add(new OracleParameter("buyerId", request.BuyerId));
                    cmd.Parameters.Add(new OracleParameter("totalAmount", totalAmount));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    cmd.Parameters.Add(idParam);
                    cmd.ExecuteNonQuery();
                    newOrderId = Convert.ToInt64(idParam.Value.ToString());
                }

                foreach (var d in request.Details)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO PURCHASE_ORDER_ITEM
                        (ORDER_ID, MATERIAL_ID, QUANTITY, RECEIVED_QTY, UNIT_PRICE)
                        VALUES (:orderId, :matId, :qty, 0, :price)";
                    cmd.Parameters.Add(new OracleParameter("orderId", newOrderId));
                    cmd.Parameters.Add(new OracleParameter("matId", d.MaterialId));
                    cmd.Parameters.Add(new OracleParameter("qty", d.Quantity));
                    cmd.Parameters.Add(new OracleParameter("price", d.UnitPrice));
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                return PurchaseResult.Fail(500, $"创建采购订单失败: {ex.Message}");
            }
        }

        return PurchaseResult.Success(GetOrderInternal(conn, newOrderId)!);
    }

    // ═══════════════════════════════════════════════════════════════
    //  按缺料生成采购订单草稿
    // ═══════════════════════════════════════════════════════════════

    public (int CreatedCount, List<PurchaseOrder> Records, List<PurchaseDraftFromShortageResponseAllOfDataUnassignedItems> UnassignedItems)
        CreateDraftsFromShortage(PurchaseDraftFromShortageRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var unassigned = new List<PurchaseDraftFromShortageResponseAllOfDataUnassignedItems>();
        var supplierGroups = new Dictionary<long, List<(long MaterialId, decimal PurchaseQty)>>();

        foreach (var item in request.Items)
        {
            long supplierId;
            if (item.SupplierId.HasValue && item.SupplierId.Value > 0)
            {
                supplierId = item.SupplierId.Value;
            }
            else
            {
                supplierId = GetDefaultSupplier(conn, item.MaterialId);
            }

            if (supplierId == 0)
            {
                unassigned.Add(new PurchaseDraftFromShortageResponseAllOfDataUnassignedItems
                {
                    MaterialId = item.MaterialId,
                    PurchaseQty = item.PurchaseQty,
                });
                continue;
            }

            if (!supplierGroups.ContainsKey(supplierId))
                supplierGroups[supplierId] = new List<(long, decimal)>();

            supplierGroups[supplierId].Add((item.MaterialId, item.PurchaseQty));
        }

        var createdOrders = new List<PurchaseOrder>();
        foreach (var (supId, items) in supplierGroups)
        {
            if (!SupplierExists(conn, supId))
            {
                foreach (var item in items)
                {
                    unassigned.Add(new PurchaseDraftFromShortageResponseAllOfDataUnassignedItems
                    {
                        MaterialId = item.MaterialId,
                        PurchaseQty = item.PurchaseQty,
                    });
                }
                continue;
            }

            using var tx = conn.BeginTransaction();
            try
            {
                decimal totalAmount = 0;
                foreach (var (matId, qty) in items)
                {
                    var price = GetCurrentPriceForMaterial(conn, matId) ?? 0m;
                    totalAmount += qty * price;
                }

                long newOrderId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO PURCHASE_ORDER
                        (STATUS, SUPPLIER_ID, ORDER_DATE, EXPECTED_DATE, BUYER_ID, TOTAL_AMOUNT)
                        VALUES (:status, :supplierId, SYSDATE, :expectedDate, :buyerId, :totalAmount)
                        RETURNING ORDER_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("status", PurchaseOrderStatusMap.Db.Draft));
                    cmd.Parameters.Add(new OracleParameter("supplierId", supId));
                    cmd.Parameters.Add(new OracleParameter("expectedDate", request.ExpectedDate.ToDateTime(TimeOnly.MinValue)));
                    cmd.Parameters.Add(new OracleParameter("buyerId", request.BuyerId));
                    cmd.Parameters.Add(new OracleParameter("totalAmount", totalAmount));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    cmd.Parameters.Add(idParam);
                    cmd.ExecuteNonQuery();
                    newOrderId = Convert.ToInt64(idParam.Value.ToString());
                }

                foreach (var (matId, qty) in items)
                {
                    var price = GetCurrentPriceForMaterial(conn, matId) ?? 0m;
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO PURCHASE_ORDER_ITEM
                        (ORDER_ID, MATERIAL_ID, QUANTITY, RECEIVED_QTY, UNIT_PRICE)
                        VALUES (:orderId, :matId, :qty, 0, :price)";
                    cmd.Parameters.Add(new OracleParameter("orderId", newOrderId));
                    cmd.Parameters.Add(new OracleParameter("matId", matId));
                    cmd.Parameters.Add(new OracleParameter("qty", qty));
                    cmd.Parameters.Add(new OracleParameter("price", price));
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                createdOrders.Add(GetOrderInternal(conn, newOrderId)!);
            }
            catch (OracleException)
            {
                tx.Rollback();
            }
        }

        return (createdOrders.Count, createdOrders, unassigned);
    }

    // ═══════════════════════════════════════════════════════════════
    //  采购订单 提交 / 取消
    // ═══════════════════════════════════════════════════════════════

    public PurchaseResult Submit(long orderId, long operatorId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawOrderStatus(conn, orderId);
        if (current is null) return PurchaseResult.Fail(404, "采购订单不存在");
        if (current != PurchaseOrderStatusMap.Db.Draft)
            return PurchaseResult.Fail(409, "仅草稿状态可提交");

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PURCHASE_ORDER
                SET STATUS = :status
                WHERE ORDER_ID = :orderId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", PurchaseOrderStatusMap.Db.Submitted));
            cmd.Parameters.Add(new OracleParameter("orderId", orderId));
            cmd.Parameters.Add(new OracleParameter("expected", PurchaseOrderStatusMap.Db.Draft));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
            return PurchaseResult.Fail(409, "订单状态已变更，请刷新后重试");

        return PurchaseResult.Success(GetOrderInternal(conn, orderId)!);
    }

    public PurchaseResult Cancel(long orderId, long operatorId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawOrderStatus(conn, orderId);
        if (current is null) return PurchaseResult.Fail(404, "采购订单不存在");
        if (current is not (PurchaseOrderStatusMap.Db.Draft or PurchaseOrderStatusMap.Db.Submitted))
            return PurchaseResult.Fail(409, "仅草稿或已提交状态可取消");

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PURCHASE_ORDER
                SET STATUS = :status
                WHERE ORDER_ID = :orderId
                  AND STATUS IN (:draft, :submitted)";
            cmd.Parameters.Add(new OracleParameter("status", PurchaseOrderStatusMap.Db.Cancelled));
            cmd.Parameters.Add(new OracleParameter("orderId", orderId));
            cmd.Parameters.Add(new OracleParameter("draft", PurchaseOrderStatusMap.Db.Draft));
            cmd.Parameters.Add(new OracleParameter("submitted", PurchaseOrderStatusMap.Db.Submitted));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
            return PurchaseResult.Fail(409, "订单状态已变更，请刷新后重试");

        return PurchaseResult.Success(GetOrderInternal(conn, orderId)!);
    }

    // ═══════════════════════════════════════════════════════════════
    //  采购收货 列表 / 登记
    // ═══════════════════════════════════════════════════════════════

    public (List<PurchaseReceipt> Records, int Total) ListReceipts(
        int page, int pageSize, long? orderId, long? materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (orderId.HasValue) where.Add("r.ORDER_ID = :orderId");
        if (materialId.HasValue) where.Add("r.MATERIAL_ID = :materialId");
        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (orderId.HasValue) cmd.Parameters.Add(new OracleParameter("orderId", orderId.Value));
            if (materialId.HasValue) cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM RECEIVE_RECORD r" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<PurchaseReceipt>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ReceiptColumns + whereClause +
                @" ORDER BY r.RECEIVE_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapReceipt(reader));
        }

        return (records, total);
    }

    public PurchaseReceiptResult AddReceipt(PurchaseReceiptCreateRequest request)
    {
        if (request.Quantity <= 0)
            return PurchaseReceiptResult.Fail(400, "收货数量必须大于 0");

        using var conn = new OracleConnection(connString);
        conn.Open();

        var orderStatus = GetRawOrderStatus(conn, request.OrderId);
        if (orderStatus is null)
            return PurchaseReceiptResult.Fail(404, "采购订单不存在");
        if (orderStatus is not (PurchaseOrderStatusMap.Db.Submitted or PurchaseOrderStatusMap.Db.PartialReceived))
            return PurchaseReceiptResult.Fail(409, "仅已提交或部分到货订单可收货");

        decimal orderedQty;
        decimal alreadyReceived;
        long itemId = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT ITEM_ID, QUANTITY, COALESCE(RECEIVED_QTY, 0)
                FROM PURCHASE_ORDER_ITEM
                WHERE ORDER_ID = :orderId AND MATERIAL_ID = :matId";
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("matId", request.MaterialId));
            using var r = cmd.ExecuteReader();
            if (!r.Read())
                return PurchaseReceiptResult.Fail(400, "该订单不存在此物料明细");
            itemId = Convert.ToInt64(r.GetValue(0));
            orderedQty = r.GetDecimal(1);
            alreadyReceived = r.GetDecimal(2);
        }

        if (alreadyReceived + request.Quantity > orderedQty)
            return PurchaseReceiptResult.Fail(409, $"收货数量超限：已收 {alreadyReceived}，本次 {request.Quantity}，订购 {orderedQty}");

        long newReceiptId;
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO RECEIVE_RECORD
                        (ORDER_ID, MATERIAL_ID, QUANTITY, RECEIVE_DATE)
                        VALUES (:orderId, :matId, :qty, :receiveDate)
                        RETURNING RECEIVE_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
                    cmd.Parameters.Add(new OracleParameter("matId", request.MaterialId));
                    cmd.Parameters.Add(new OracleParameter("qty", request.Quantity));
                    cmd.Parameters.Add(new OracleParameter("receiveDate", request.ReceiveDate.ToDateTime(TimeOnly.MinValue)));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    cmd.Parameters.Add(idParam);
                    cmd.ExecuteNonQuery();
                    newReceiptId = Convert.ToInt64(idParam.Value.ToString());
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE PURCHASE_ORDER_ITEM
                        SET RECEIVED_QTY = RECEIVED_QTY + :qty
                        WHERE ITEM_ID = :itemId";
                    cmd.Parameters.Add(new OracleParameter("qty", request.Quantity));
                    cmd.Parameters.Add(new OracleParameter("itemId", itemId));
                    cmd.ExecuteNonQuery();
                }

                var newReceived = alreadyReceived + request.Quantity;
                var newStatus = newReceived >= orderedQty
                    ? PurchaseOrderStatusMap.Db.Completed
                    : (newReceived > 0 ? PurchaseOrderStatusMap.Db.PartialReceived : PurchaseOrderStatusMap.Db.Submitted);

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE PURCHASE_ORDER
                        SET STATUS = :newStatus, ACTUAL_DATE = CASE WHEN :checkCompleted = :completed THEN SYSDATE ELSE ACTUAL_DATE END
                        WHERE ORDER_ID = :orderId";
                    cmd.Parameters.Add(new OracleParameter("newStatus", newStatus));
                    cmd.Parameters.Add(new OracleParameter("checkCompleted", newStatus));
                    cmd.Parameters.Add(new OracleParameter("completed", PurchaseOrderStatusMap.Db.Completed));
                    cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
                    cmd.ExecuteNonQuery();
                }

                EnsureMaterialStockExists(conn, request.MaterialId, tx);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE MATERIAL_STOCK
                        SET AVAILABLE_QTY = AVAILABLE_QTY + :qty,
                            LAST_IN_DATE = SYSDATE
                        WHERE MATERIAL_ID = :matId";
                    cmd.Parameters.Add(new OracleParameter("qty", request.Quantity));
                    cmd.Parameters.Add(new OracleParameter("matId", request.MaterialId));
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                return PurchaseReceiptResult.Fail(500, $"收货失败: {ex.Message}");
            }
        }

        return PurchaseReceiptResult.Success(GetReceiptInternal(conn, newReceiptId)!);
    }

    // ═══════════════════════════════════════════════════════════════
    //  逾期提醒 生成 / 列表 / 处理
    // ═══════════════════════════════════════════════════════════════

    public (int GeneratedCount, List<PurchaseOverdueReminder> Records) GenerateReminders(long? orderId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var reminders = new List<PurchaseOverdueReminder>();
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        string filterClause = orderId.HasValue ? "AND o.ORDER_ID = :orderId" : "";

        using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = $@"
                SELECT o.ORDER_ID, o.EXPECTED_DATE
                FROM PURCHASE_ORDER o
                WHERE o.STATUS NOT IN (:completed, :cancelled)
                  AND o.EXPECTED_DATE < :today
                  {filterClause}";
            queryCmd.Parameters.Add(new OracleParameter("completed", PurchaseOrderStatusMap.Db.Completed));
            queryCmd.Parameters.Add(new OracleParameter("cancelled", PurchaseOrderStatusMap.Db.Cancelled));
            queryCmd.Parameters.Add(new OracleParameter("today", today.ToDateTime(TimeOnly.MinValue)));
            if (orderId.HasValue)
                queryCmd.Parameters.Add(new OracleParameter("orderId", orderId.Value));

            using var reader = queryCmd.ExecuteReader();
            var candidates = new List<(long OrderId, DateOnly ExpectedDate)>();
            while (reader.Read())
            {
                candidates.Add((Convert.ToInt64(reader.GetValue(0)),
                    DateOnly.FromDateTime(reader.GetDateTime(1))));
            }
            reader.Close();

            foreach (var (ordId, expectedDate) in candidates)
            {
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = @"SELECT COUNT(*) FROM OVERDUE_REMINDER
                    WHERE ORDER_ID = :orderId AND STATUS = :pendingUrge";
                checkCmd.Parameters.Add(new OracleParameter("orderId", ordId));
                checkCmd.Parameters.Add(new OracleParameter("pendingUrge", PurchaseOverdueReminderStatusMap.Db.PendingUrge));
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    continue;

                int overdueDays = (today.DayNumber - expectedDate.DayNumber);

                long newId;
                using (var insCmd = conn.CreateCommand())
                {
                    insCmd.CommandText = @"INSERT INTO OVERDUE_REMINDER
                        (ORDER_ID, EXPECTED_DATE, OVERDUE_DAYS, REMIND_TIME, STATUS)
                        VALUES (:orderId, :expectedDate, :overdueDays, :remindTime, :status)
                        RETURNING REMINDER_ID INTO :newId";
                    insCmd.Parameters.Add(new OracleParameter("orderId", ordId));
                    insCmd.Parameters.Add(new OracleParameter("expectedDate", expectedDate.ToDateTime(TimeOnly.MinValue)));
                    insCmd.Parameters.Add(new OracleParameter("overdueDays", overdueDays));
                    insCmd.Parameters.Add(new OracleParameter("remindTime", now));
                    insCmd.Parameters.Add(new OracleParameter("status", PurchaseOverdueReminderStatusMap.Db.PendingUrge));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    insCmd.Parameters.Add(idParam);
                    insCmd.ExecuteNonQuery();
                    newId = Convert.ToInt64(idParam.Value.ToString());
                }

                reminders.Add(new PurchaseOverdueReminder
                {
                    ReminderId = newId,
                    OrderId = ordId,
                    ExpectedDate = expectedDate,
                    OverdueDays = overdueDays,
                    RemindTime = now,
                    Status = PurchaseOverdueReminderStatus.PendingUrgeEnum,
                    Remark = null!,
                });
            }
        }

        return (reminders.Count, reminders);
    }

    public (List<PurchaseOverdueReminder> Records, int Total) ListReminders(
        int page, int pageSize, long? orderId, string? dbStatus)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (orderId.HasValue) where.Add("r.ORDER_ID = :orderId");
        if (!string.IsNullOrEmpty(dbStatus)) where.Add("r.STATUS = :status");
        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (orderId.HasValue) cmd.Parameters.Add(new OracleParameter("orderId", orderId.Value));
            if (!string.IsNullOrEmpty(dbStatus)) cmd.Parameters.Add(new OracleParameter("status", dbStatus));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM OVERDUE_REMINDER r" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<PurchaseOverdueReminder>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ReminderColumns + whereClause +
                @" ORDER BY r.REMINDER_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapReminder(reader));
        }

        return (records, total);
    }

    public ReminderResult HandleReminder(long reminderId, string status, string? remark)
    {
        if (status != "urged" && status != "received")
            return ReminderResult.Fail(400, "状态必须为 urged 或 received");

        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawReminderStatus(conn, reminderId);
        if (current is null) return ReminderResult.Fail(404, "逾期提醒不存在");

        var dbStatus = status switch
        {
            "urged" => PurchaseOverdueReminderStatusMap.Db.Urged,
            "received" => PurchaseOverdueReminderStatusMap.Db.Received,
            _ => "",
        };

        if (current == PurchaseOverdueReminderStatusMap.Db.Received)
            return ReminderResult.Fail(409, "已到货提醒不可再次处理");

        if (current == PurchaseOverdueReminderStatusMap.Db.Urged && status == "urged")
            return ReminderResult.Fail(409, "已催交提醒不可重复催交");

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE OVERDUE_REMINDER
                SET STATUS = :status, REMARK = :remark
                WHERE REMINDER_ID = :reminderId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            cmd.Parameters.Add(new OracleParameter("remark", string.IsNullOrWhiteSpace(remark) ? DBNull.Value : remark.Trim()));
            cmd.Parameters.Add(new OracleParameter("reminderId", reminderId));
            cmd.Parameters.Add(new OracleParameter("expected", current));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
            return ReminderResult.Fail(409, "提醒状态已变更，请刷新后重试");

        return ReminderResult.Success(GetReminderInternal(conn, reminderId)!);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════════════════════

    private PurchaseOrder MapOrder(OracleDataReader reader, OracleConnection conn)
    {
        var orderId = Convert.ToInt64(reader.GetValue(0));
        var statusDb = reader.GetString(1);
        var status = PurchaseOrderStatusMap.FromDb(statusDb);

        var supplier = new SupplierDetail
        {
            SupplierId = Convert.ToInt64(reader.GetValue(2)),
            SupplierName = reader.IsDBNull(3) ? null! : reader.GetString(3),
            ContactPerson = reader.IsDBNull(4) ? null! : reader.GetString(4),
            ContactPhone = reader.IsDBNull(5) ? null! : reader.GetString(5),
        };

        var details = GetOrderItems(conn, orderId);
        decimal totalReceived = details.Sum(d => d.ReceivedQty);
        decimal totalOrdered = details.Sum(d => d.Quantity);
        decimal receiveProgress = totalOrdered > 0 ? totalReceived / totalOrdered : 0;

        bool isOverdue = statusDb is not (PurchaseOrderStatusMap.Db.Completed or PurchaseOrderStatusMap.Db.Cancelled)
            && !reader.IsDBNull(7)
            && DateOnly.FromDateTime(reader.GetDateTime(7)) < DateOnly.FromDateTime(DateTime.Now);

        int overdueDays = 0;
        if (isOverdue && !reader.IsDBNull(7))
            overdueDays = (DateOnly.FromDateTime(DateTime.Now).DayNumber
                - DateOnly.FromDateTime(reader.GetDateTime(7)).DayNumber);

        return new PurchaseOrder
        {
            OrderId = orderId,
            Status = status,
            Supplier = supplier,
            OrderDate = reader.IsDBNull(6) ? default : DateOnly.FromDateTime(reader.GetDateTime(6)),
            ExpectedDate = reader.IsDBNull(7) ? default : DateOnly.FromDateTime(reader.GetDateTime(7)),
            ActualDate = reader.IsDBNull(8) ? null : DateOnly.FromDateTime(reader.GetDateTime(8)),
            BuyerId = Convert.ToInt64(reader.GetValue(9)),
            TotalAmount = reader.GetDecimal(10),
            ReceiveProgress = receiveProgress,
            IsOverdue = isOverdue,
            OverdueDays = overdueDays,
            Details = details,
        };
    }

    private List<PurchaseOrderDetailLine> GetOrderItems(OracleConnection conn, long orderId)
    {
        var items = new List<PurchaseOrderDetailLine>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = ItemColumns + " WHERE i.ORDER_ID = :orderId ORDER BY i.ITEM_ID";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var qty = reader.GetDecimal(4);
            var received = reader.GetDecimal(5);
            var price = reader.GetDecimal(6);

            items.Add(new PurchaseOrderDetailLine
            {
                ItemId = Convert.ToInt64(reader.GetValue(0)),
                OrderId = Convert.ToInt64(reader.GetValue(1)),
                MaterialId = Convert.ToInt64(reader.GetValue(2)),
                MaterialName = reader.IsDBNull(3) ? null! : reader.GetString(3),
                Quantity = qty,
                ReceivedQty = received,
                UnitPrice = price,
                LineAmount = qty * price,
                ReceiveProgress = qty > 0 ? received / qty : 0,
            });
        }

        return items;
    }

    private static PurchaseReceipt MapReceipt(OracleDataReader reader) => new()
    {
        ReceiveId = Convert.ToInt64(reader.GetValue(0)),
        OrderId = Convert.ToInt64(reader.GetValue(1)),
        MaterialId = Convert.ToInt64(reader.GetValue(2)),
        MaterialName = reader.IsDBNull(3) ? null! : reader.GetString(3),
        Quantity = reader.GetDecimal(4),
        ReceiveDate = DateOnly.FromDateTime(reader.GetDateTime(5)),
    };

    private static PurchaseOverdueReminder MapReminder(OracleDataReader reader) => new()
    {
        ReminderId = Convert.ToInt64(reader.GetValue(0)),
        OrderId = Convert.ToInt64(reader.GetValue(1)),
        ExpectedDate = reader.IsDBNull(2) ? default : DateOnly.FromDateTime(reader.GetDateTime(2)),
        OverdueDays = Convert.ToInt32(reader.GetValue(3)),
        RemindTime = reader.GetDateTime(4),
        Status = PurchaseOverdueReminderStatusMap.FromDb(reader.GetString(5)),
        Remark = reader.IsDBNull(6) ? null! : reader.GetString(6),
    };

    private PurchaseOrder? GetOrderInternal(OracleConnection conn, long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = OrderColumns + " WHERE o.ORDER_ID = :orderId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapOrder(reader, conn) : null;
    }

    private PurchaseReceipt? GetReceiptInternal(OracleConnection conn, long receiptId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = ReceiptColumns + " WHERE r.RECEIVE_ID = :receiptId";
        cmd.Parameters.Add(new OracleParameter("receiptId", receiptId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapReceipt(reader) : null;
    }

    private PurchaseOverdueReminder? GetReminderInternal(OracleConnection conn, long reminderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = ReminderColumns + " WHERE r.REMINDER_ID = :reminderId";
        cmd.Parameters.Add(new OracleParameter("reminderId", reminderId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapReminder(reader) : null;
    }

    private static string? GetRawOrderStatus(OracleConnection conn, long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM PURCHASE_ORDER WHERE ORDER_ID = :orderId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static string? GetRawReminderStatus(OracleConnection conn, long reminderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM OVERDUE_REMINDER WHERE REMINDER_ID = :reminderId";
        cmd.Parameters.Add(new OracleParameter("reminderId", reminderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static bool MaterialExists(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM MATERIAL WHERE MATERIAL_ID = :matId";
        cmd.Parameters.Add(new OracleParameter("matId", materialId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool SupplierExists(OracleConnection conn, long supplierId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SUPPLIER WHERE SUPPLIER_ID = :supId";
        cmd.Parameters.Add(new OracleParameter("supId", supplierId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static long GetDefaultSupplier(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DEFAULT_SUPPLIER_ID FROM MATERIAL WHERE MATERIAL_ID = :matId";
        cmd.Parameters.Add(new OracleParameter("matId", materialId));
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    private static decimal? GetCurrentPriceForMaterial(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT PRICE FROM SUPPLIER_PRICE
            WHERE MATERIAL_ID = :matId
              AND VALID_FROM <= SYSDATE
              AND (VALID_TO IS NULL OR VALID_TO >= SYSDATE)
            ORDER BY VALID_FROM DESC
            FETCH FIRST 1 ROW ONLY";
        cmd.Parameters.Add(new OracleParameter("matId", materialId));
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToDecimal(result);
    }

    private static void EnsureMaterialStockExists(OracleConnection conn, long materialId, OracleTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"MERGE INTO MATERIAL_STOCK ms
            USING (SELECT :matId AS MAT_ID FROM DUAL) d
            ON (ms.MATERIAL_ID = d.MAT_ID)
            WHEN NOT MATCHED THEN
                INSERT (MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY)
                VALUES (:matId2, 0, 0)";
        cmd.Parameters.Add(new OracleParameter("matId", materialId));
        cmd.Parameters.Add(new OracleParameter("matId2", materialId));
        cmd.ExecuteNonQuery();
    }
}
