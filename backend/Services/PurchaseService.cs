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

public sealed record PurchaseDraftResult(
    bool Ok,
    int CreatedCount,
    List<PurchaseOrder> Records,
    List<PurchaseDraftFromShortageResponseAllOfDataUnassignedItems> UnassignedItems,
    int ErrorCode,
    string? ErrorMessage)
{
    public static PurchaseDraftResult Success(
        List<PurchaseOrder> records,
        List<PurchaseDraftFromShortageResponseAllOfDataUnassignedItems> unassignedItems) =>
        new(true, records.Count, records, unassignedItems, 200, null);

    public static PurchaseDraftResult Fail(int code, string message) =>
        new(
            false,
            0,
            new List<PurchaseOrder>(),
            new List<PurchaseDraftFromShortageResponseAllOfDataUnassignedItems>(),
            code,
            message);
}

/// <summary>
/// 采购管理主责 Service（B 模块）。维护 purchase_order、purchase_order_item、
/// receive_record、overdue_reminder 表。
/// </summary>
public class PurchaseService(string connString, ILogger<PurchaseService> logger)
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

        // 结束日期包含整天；最大日期无需上界，避免计算次日时溢出。
        var orderDateEndExclusive = orderDateEnd.HasValue && orderDateEnd.Value < DateOnly.MaxValue
            ? orderDateEnd.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : (DateTime?)null;

        if (supplierId.HasValue) where.Add("o.SUPPLIER_ID = :supplierId");
        if (!string.IsNullOrEmpty(dbStatus)) where.Add("o.STATUS = :status");
        if (orderDateStart.HasValue) where.Add("o.ORDER_DATE >= :orderDateStart");
        if (orderDateEndExclusive.HasValue) where.Add("o.ORDER_DATE < :orderDateEndExclusive");
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
            if (orderDateEndExclusive.HasValue) cmd.Parameters.Add(new OracleParameter("orderDateEndExclusive", orderDateEndExclusive.Value));
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
            while (reader.Read()) records.Add(MapOrder(reader, []));
        }

        var detailsByOrder = GetOrderItems(conn, records.Select(record => record.OrderId));
        foreach (var record in records)
        {
            ApplyOrderDetails(
                record,
                detailsByOrder.GetValueOrDefault(record.OrderId) ?? []);
        }

        return (records, total);
    }

    public (List<SupplierDetail> Records, int Total) ListSuppliers(
        int page,
        int pageSize,
        long? supplierId,
        string? supplierName)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (supplierId.HasValue) where.Add("s.SUPPLIER_ID = :supplierId");
        if (!string.IsNullOrWhiteSpace(supplierName))
            where.Add("s.SUPPLIER_NAME LIKE :supplierName");
        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;

        void AddFilters(OracleCommand command)
        {
            if (supplierId.HasValue)
                command.Parameters.Add(new OracleParameter("supplierId", supplierId.Value));
            if (!string.IsNullOrWhiteSpace(supplierName))
                command.Parameters.Add(new OracleParameter("supplierName", $"%{supplierName.Trim()}%"));
        }

        int total;
        using (var countCommand = conn.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM SUPPLIER s" + whereClause;
            AddFilters(countCommand);
            total = Convert.ToInt32(countCommand.ExecuteScalar());
        }

        var records = new List<SupplierDetail>();
        using (var command = conn.CreateCommand())
        {
            command.CommandText = @"SELECT s.SUPPLIER_ID, s.SUPPLIER_NAME,
                                           s.CONTACT_PERSON, s.CONTACT_PHONE
                                    FROM SUPPLIER s" + whereClause +
                @" ORDER BY s.SUPPLIER_ID
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(command);
            command.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            command.Parameters.Add(new OracleParameter("take", pageSize));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new SupplierDetail
                {
                    SupplierId = Convert.ToInt64(reader.GetValue(0)),
                    SupplierName = reader.GetString(1),
                    ContactPerson = reader.IsDBNull(2) ? null! : reader.GetString(2),
                    ContactPhone = reader.IsDBNull(3) ? null! : reader.GetString(3),
                });
            }
        }

        return (records, total);
    }

    public (List<PurchaseBuyerBrief> Records, int Total) ListBuyers(
        int page,
        int pageSize,
        long? buyerId,
        string? buyerName)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>
        {
            "u.STATUS = 'valid'",
            "r.STATUS = 'valid'",
            "r.ROLE_NAME IN ('采购员', '采购主管', '系统管理员')",
        };
        if (buyerId.HasValue) where.Add("u.USER_ID = :buyerId");
        if (!string.IsNullOrWhiteSpace(buyerName)) where.Add("u.USER_NAME LIKE :buyerName");
        var whereClause = " WHERE " + string.Join(" AND ", where);
        const string fromClause = @" FROM SYS_USER u
            JOIN SYS_USER_ROLE ur ON ur.USER_ID = u.USER_ID
            JOIN SYS_ROLE r ON r.ROLE_ID = ur.ROLE_ID";

        void AddFilters(OracleCommand command)
        {
            if (buyerId.HasValue)
                command.Parameters.Add(new OracleParameter("buyerId", buyerId.Value));
            if (!string.IsNullOrWhiteSpace(buyerName))
                command.Parameters.Add(new OracleParameter("buyerName", $"%{buyerName.Trim()}%"));
        }

        int total;
        using (var countCommand = conn.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(DISTINCT u.USER_ID)" + fromClause + whereClause;
            AddFilters(countCommand);
            total = Convert.ToInt32(countCommand.ExecuteScalar());
        }

        var records = new List<PurchaseBuyerBrief>();
        using (var command = conn.CreateCommand())
        {
            command.CommandText = "SELECT DISTINCT u.USER_ID, u.USER_NAME" + fromClause + whereClause +
                @" ORDER BY u.USER_ID
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(command);
            command.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            command.Parameters.Add(new OracleParameter("take", pageSize));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new PurchaseBuyerBrief
                {
                    BuyerId = Convert.ToInt64(reader.GetValue(0)),
                    BuyerName = reader.GetString(1),
                });
            }
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
        }

        var materialIds = request.Details.Select(detail => detail.MaterialId).Distinct().ToList();
        var existingMaterialIds = GetMaterialDefaultSuppliers(conn, materialIds).Keys.ToHashSet();
        var missingMaterialIds = materialIds.Where(id => !existingMaterialIds.Contains(id)).ToList();
        if (missingMaterialIds.Count > 0)
            return PurchaseResult.Fail(400, $"物料 {missingMaterialIds[0]} 不存在");

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
                        VALUES (:status, :supplierId, TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)), :expectedDate, :buyerId, :totalAmount)
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

                InsertPurchaseItems(
                    conn,
                    tx,
                    newOrderId,
                    request.Details.Select(detail =>
                        (detail.MaterialId, detail.Quantity, detail.UnitPrice)).ToList());

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

    public PurchaseDraftResult CreateDraftsFromShortage(PurchaseDraftFromShortageRequest request)
    {
        using var conn = new OracleConnection(connString);
        var unassigned = new List<PurchaseDraftFromShortageResponseAllOfDataUnassignedItems>();
        var supplierGroups = new Dictionary<long, List<(long MaterialId, decimal PurchaseQty)>>();
        OracleTransaction? tx = null;

        try
        {
            conn.Open();

            foreach (var item in request.Items)
            {
                if (item.PurchaseQty <= 0)
                    return PurchaseDraftResult.Fail(400, $"物料 {item.MaterialId} 采购数量必须大于 0");
            }

            var materialIds = request.Items.Select(item => item.MaterialId).Distinct().ToList();
            var defaultSuppliers = GetMaterialDefaultSuppliers(conn, materialIds);
            var missingMaterialIds = materialIds.Where(id => !defaultSuppliers.ContainsKey(id)).ToList();
            if (missingMaterialIds.Count > 0)
                return PurchaseDraftResult.Fail(400, $"物料 {missingMaterialIds[0]} 不存在");

            var resolvedItems = request.Items.Select(item =>
            {
                var supplierId = item.SupplierId is > 0
                    ? item.SupplierId.Value
                    : defaultSuppliers[item.MaterialId] ?? 0;
                return (item.MaterialId, item.PurchaseQty, SupplierId: supplierId);
            }).ToList();
            var existingSupplierIds = GetExistingSupplierIds(
                conn,
                resolvedItems.Where(item => item.SupplierId > 0).Select(item => item.SupplierId));

            foreach (var item in resolvedItems)
            {
                long supplierId;
                supplierId = item.SupplierId;

                if (supplierId == 0 || !existingSupplierIds.Contains(supplierId))
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

            if (supplierGroups.Count == 0)
                return PurchaseDraftResult.Success(new List<PurchaseOrder>(), unassigned);

            var createdOrderIds = new List<long>();
            tx = conn.BeginTransaction();
            var prices = GetCurrentPrices(
                conn,
                tx,
                supplierGroups.SelectMany(group =>
                    group.Value.Select(item => (item.MaterialId, SupplierId: group.Key))));

            foreach (var (supId, items) in supplierGroups)
            {
                var pricedItems = new List<(long MaterialId, decimal PurchaseQty, decimal UnitPrice)>();
                decimal totalAmount = 0;

                foreach (var (materialId, qty) in items)
                {
                    var price = prices.GetValueOrDefault((materialId, supId));
                    pricedItems.Add((materialId, qty, price));
                    totalAmount += qty * price;
                }

                long newOrderId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO PURCHASE_ORDER
                        (STATUS, SUPPLIER_ID, ORDER_DATE, EXPECTED_DATE, BUYER_ID, TOTAL_AMOUNT)
                        VALUES (:status, :supplierId, TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)), :expectedDate, :buyerId, :totalAmount)
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

                InsertPurchaseItems(conn, tx, newOrderId, pricedItems);
                createdOrderIds.Add(newOrderId);
            }

            var createdOrders = GetOrdersInternal(conn, createdOrderIds, tx);
            if (createdOrders.Count != createdOrderIds.Count)
            {
                tx.Rollback();
                return PurchaseDraftResult.Fail(500, "生成采购订单草稿失败，请稍后重试");
            }

            tx.Commit();
            return PurchaseDraftResult.Success(createdOrders, unassigned);
        }
        catch (OracleException ex)
        {
            if (tx is not null)
            {
                try
                {
                    tx.Rollback();
                }
                catch (OracleException rollbackException)
                {
                    logger.LogError(rollbackException, "回滚采购订单草稿事务失败");
                }
            }

            logger.LogError(ex, "按缺料生成采购订单草稿失败");
            return PurchaseDraftResult.Fail(500, "生成采购订单草稿失败，请稍后重试");
        }
        finally
        {
            tx?.Dispose();
        }
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

        long itemId = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT ITEM_ID
                FROM PURCHASE_ORDER_ITEM
                WHERE ORDER_ID = :orderId AND MATERIAL_ID = :materialId";
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
            using var r = cmd.ExecuteReader();
            if (!r.Read())
                return PurchaseReceiptResult.Fail(400, "该订单不存在此物料明细");
            itemId = Convert.ToInt64(r.GetValue(0));
        }

        long newReceiptId;
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                int affected;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE PURCHASE_ORDER_ITEM
                        SET RECEIVED_QTY = RECEIVED_QTY + :incrementQty
                        WHERE ITEM_ID = :itemId
                          AND RECEIVED_QTY + :limitQty <= QUANTITY";
                    cmd.Parameters.Add(new OracleParameter("incrementQty", request.Quantity));
                    cmd.Parameters.Add(new OracleParameter("itemId", itemId));
                    cmd.Parameters.Add(new OracleParameter("limitQty", request.Quantity));
                    affected = cmd.ExecuteNonQuery();
                }

                if (affected == 0)
                {
                    tx.Rollback();
                    return PurchaseReceiptResult.Fail(409, "收货数量超限或采购明细已变更，请刷新后重试");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO RECEIVE_RECORD
                        (ORDER_ID, MATERIAL_ID, QUANTITY, RECEIVE_DATE)
                        VALUES (:orderId,  :materialId, :qty, :receiveDate)
                        RETURNING RECEIVE_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
                    cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
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

                var newStatus = IsOrderFullyReceived(conn, tx, request.OrderId)
                    ? PurchaseOrderStatusMap.Db.Completed
                    : PurchaseOrderStatusMap.Db.PartialReceived;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE PURCHASE_ORDER
                        SET STATUS = :newStatus, ACTUAL_DATE = CASE WHEN :checkCompleted = :completed THEN TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)) ELSE ACTUAL_DATE END
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
                            LAST_IN_DATE = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                        WHERE MATERIAL_ID = :materialId";
                    cmd.Parameters.Add(new OracleParameter("qty", request.Quantity));
                    cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
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
        var now = DateTime.UtcNow;
        var today = BusinessTime.ToDate(now);

        string filterClause = orderId.HasValue ? "AND o.ORDER_ID = :orderId" : "";

        using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = $@"
                SELECT o.ORDER_ID, o.EXPECTED_DATE
                FROM PURCHASE_ORDER o
                WHERE o.STATUS NOT IN (:completed, :cancelled)
                  AND o.EXPECTED_DATE < :today
                  AND NOT EXISTS (
                      SELECT 1
                      FROM OVERDUE_REMINDER r
                      WHERE r.ORDER_ID = o.ORDER_ID
                        AND r.STATUS = :pendingUrge)
                  {filterClause}";
            queryCmd.Parameters.Add(new OracleParameter("completed", PurchaseOrderStatusMap.Db.Completed));
            queryCmd.Parameters.Add(new OracleParameter("cancelled", PurchaseOrderStatusMap.Db.Cancelled));
            queryCmd.Parameters.Add(new OracleParameter("today", today.ToDateTime(TimeOnly.MinValue)));
            queryCmd.Parameters.Add(new OracleParameter(
                "pendingUrge",
                PurchaseOverdueReminderStatusMap.Db.PendingUrge));
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

    private static PurchaseOrder MapOrder(
        OracleDataReader reader,
        List<PurchaseOrderDetailLine> details)
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

        decimal totalReceived = details.Sum(d => d.ReceivedQty);
        decimal totalOrdered = details.Sum(d => d.Quantity);
        decimal receiveProgress = totalOrdered > 0 ? totalReceived / totalOrdered : 0;

        bool isOverdue = statusDb is not (PurchaseOrderStatusMap.Db.Completed or PurchaseOrderStatusMap.Db.Cancelled)
            && !reader.IsDBNull(7)
            && DateOnly.FromDateTime(reader.GetDateTime(7)) < BusinessTime.Today;

        int overdueDays = 0;
        if (isOverdue && !reader.IsDBNull(7))
            overdueDays = (BusinessTime.Today.DayNumber
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

    private static List<PurchaseOrderDetailLine> GetOrderItems(
        OracleConnection conn,
        long orderId,
        OracleTransaction? tx = null)
    {
        var items = new List<PurchaseOrderDetailLine>();
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
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

    private static Dictionary<long, List<PurchaseOrderDetailLine>> GetOrderItems(
        OracleConnection conn,
        IEnumerable<long> orderIds,
        OracleTransaction? tx = null)
    {
        var ids = orderIds.Distinct().ToList();
        var itemsByOrder = ids.ToDictionary(id => id, _ => new List<PurchaseOrderDetailLine>());
        if (ids.Count == 0)
        {
            return itemsByOrder;
        }

        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        var placeholders = new List<string>();
        for (var index = 0; index < ids.Count; index++)
        {
            var name = $"orderId{index}";
            placeholders.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, ids[index]));
        }

        cmd.CommandText = ItemColumns +
            $" WHERE i.ORDER_ID IN ({string.Join(", ", placeholders)}) ORDER BY i.ORDER_ID, i.ITEM_ID";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var item = MapOrderItem(reader);
            itemsByOrder[item.OrderId].Add(item);
        }

        return itemsByOrder;
    }

    private static List<PurchaseOrder> GetOrdersInternal(
        OracleConnection conn,
        IReadOnlyList<long> orderIds,
        OracleTransaction? tx = null)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        var placeholders = new List<string>();
        for (var index = 0; index < orderIds.Count; index++)
        {
            var name = $"createdOrderId{index}";
            placeholders.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, orderIds[index]));
        }

        cmd.CommandText = OrderColumns +
            $" WHERE o.ORDER_ID IN ({string.Join(", ", placeholders)}) ORDER BY o.ORDER_ID";
        var orders = new List<PurchaseOrder>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                orders.Add(MapOrder(reader, []));
            }
        }

        var detailsByOrder = GetOrderItems(conn, orderIds, tx);
        foreach (var order in orders)
        {
            ApplyOrderDetails(order, detailsByOrder.GetValueOrDefault(order.OrderId) ?? []);
        }

        return orders;
    }

    private static PurchaseOrderDetailLine MapOrderItem(OracleDataReader reader)
    {
        var qty = reader.GetDecimal(4);
        var received = reader.GetDecimal(5);
        var price = reader.GetDecimal(6);
        return new PurchaseOrderDetailLine
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
        };
    }

    private static void ApplyOrderDetails(
        PurchaseOrder order,
        List<PurchaseOrderDetailLine> details)
    {
        order.Details = details;
        var totalOrdered = details.Sum(detail => detail.Quantity);
        order.ReceiveProgress = totalOrdered > 0
            ? details.Sum(detail => detail.ReceivedQty) / totalOrdered
            : 0;
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
        RemindTime = reader.GetUtcDateTime(4),
        Status = PurchaseOverdueReminderStatusMap.FromDb(reader.GetString(5)),
        Remark = reader.IsDBNull(6) ? null! : reader.GetString(6),
    };

    private static PurchaseOrder? GetOrderInternal(
        OracleConnection conn,
        long orderId,
        OracleTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText = OrderColumns + " WHERE o.ORDER_ID = :orderId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var order = MapOrder(reader, []);
        reader.Close();
        ApplyOrderDetails(order, GetOrderItems(conn, orderId, tx));
        return order;
    }

    private static PurchaseReceipt? GetReceiptInternal(OracleConnection conn, long receiptId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = ReceiptColumns + " WHERE r.RECEIVE_ID = :receiptId";
        cmd.Parameters.Add(new OracleParameter("receiptId", receiptId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapReceipt(reader) : null;
    }

    private static PurchaseOverdueReminder? GetReminderInternal(OracleConnection conn, long reminderId)
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

    private static bool IsOrderFullyReceived(
        OracleConnection conn,
        OracleTransaction tx,
        long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"SELECT COUNT(*)
            FROM PURCHASE_ORDER_ITEM
            WHERE ORDER_ID = :orderId
              AND NVL(RECEIVED_QTY, 0) < QUANTITY";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        return Convert.ToInt32(cmd.ExecuteScalar()) == 0;
    }

    private static string? GetRawReminderStatus(OracleConnection conn, long reminderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM OVERDUE_REMINDER WHERE REMINDER_ID = :reminderId";
        cmd.Parameters.Add(new OracleParameter("reminderId", reminderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static Dictionary<long, long?> GetMaterialDefaultSuppliers(
        OracleConnection conn,
        IReadOnlyList<long> materialIds)
    {
        var result = new Dictionary<long, long?>();
        if (materialIds.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        var placeholders = new List<string>();
        for (var index = 0; index < materialIds.Count; index++)
        {
            var name = $"materialId{index}";
            placeholders.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, materialIds[index]));
        }

        cmd.CommandText = $@"SELECT MATERIAL_ID, DEFAULT_SUPPLIER_ID
                             FROM MATERIAL
                             WHERE MATERIAL_ID IN ({string.Join(", ", placeholders)})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[Convert.ToInt64(reader.GetValue(0))] = reader.IsDBNull(1)
                ? null
                : Convert.ToInt64(reader.GetValue(1));
        }

        return result;
    }

    private static bool SupplierExists(OracleConnection conn, long supplierId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SUPPLIER WHERE SUPPLIER_ID = :supId";
        cmd.Parameters.Add(new OracleParameter("supId", supplierId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static HashSet<long> GetExistingSupplierIds(
        OracleConnection conn,
        IEnumerable<long> supplierIds)
    {
        var ids = supplierIds.Distinct().ToList();
        var result = new HashSet<long>();
        if (ids.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        var placeholders = new List<string>();
        for (var index = 0; index < ids.Count; index++)
        {
            var name = $"supplierId{index}";
            placeholders.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, ids[index]));
        }

        cmd.CommandText = $"SELECT SUPPLIER_ID FROM SUPPLIER WHERE SUPPLIER_ID IN ({string.Join(", ", placeholders)})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Convert.ToInt64(reader.GetValue(0)));
        }

        return result;
    }

    private static Dictionary<(long MaterialId, long SupplierId), decimal> GetCurrentPrices(
        OracleConnection conn,
        OracleTransaction tx,
        IEnumerable<(long MaterialId, long SupplierId)> materialSuppliers)
    {
        var pairs = materialSuppliers.Distinct().ToList();
        var result = new Dictionary<(long MaterialId, long SupplierId), decimal>();
        if (pairs.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        var pairFilters = new List<string>();
        for (var index = 0; index < pairs.Count; index++)
        {
            var materialName = $"priceMaterialId{index}";
            var supplierName = $"priceSupplierId{index}";
            pairFilters.Add($"(MATERIAL_ID = :{materialName} AND SUPPLIER_ID = :{supplierName})");
            cmd.Parameters.Add(new OracleParameter(materialName, pairs[index].MaterialId));
            cmd.Parameters.Add(new OracleParameter(supplierName, pairs[index].SupplierId));
        }

        cmd.CommandText = $@"
            SELECT MATERIAL_ID, SUPPLIER_ID, PRICE
            FROM (
                SELECT MATERIAL_ID, SUPPLIER_ID, PRICE,
                       ROW_NUMBER() OVER (
                           PARTITION BY MATERIAL_ID, SUPPLIER_ID
                           ORDER BY VALID_FROM DESC) AS RN
                FROM SUPPLIER_PRICE
                WHERE VALID_FROM <= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                  AND (VALID_TO IS NULL OR VALID_TO >= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
                  AND ({string.Join(" OR ", pairFilters)})
            )
            WHERE RN = 1";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToInt64(reader.GetValue(1)))] = reader.GetDecimal(2);
        }

        return result;
    }

    private static void InsertPurchaseItems(
        OracleConnection conn,
        OracleTransaction tx,
        long orderId,
        IReadOnlyList<(long MaterialId, decimal Quantity, decimal UnitPrice)> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.ArrayBindCount = items.Count;
        cmd.CommandText = @"INSERT INTO PURCHASE_ORDER_ITEM
            (ORDER_ID, MATERIAL_ID, QUANTITY, RECEIVED_QTY, UNIT_PRICE)
            VALUES (:orderId, :materialId, :qty, 0, :price)";
        cmd.Parameters.Add("orderId", OracleDbType.Int64).Value =
            Enumerable.Repeat(orderId, items.Count).ToArray();
        cmd.Parameters.Add("materialId", OracleDbType.Int64).Value =
            items.Select(item => item.MaterialId).ToArray();
        cmd.Parameters.Add("qty", OracleDbType.Decimal).Value =
            items.Select(item => item.Quantity).ToArray();
        cmd.Parameters.Add("price", OracleDbType.Decimal).Value =
            items.Select(item => item.UnitPrice).ToArray();
        cmd.ExecuteNonQuery();
    }

    private static void EnsureMaterialStockExists(OracleConnection conn, long materialId, OracleTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"MERGE INTO MATERIAL_STOCK ms
            USING (SELECT :materialId AS MAT_ID FROM DUAL) d
            ON (ms.MATERIAL_ID = d.MAT_ID)
            WHEN NOT MATCHED THEN
                INSERT (MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY)
                VALUES (:matId2, 0, 0)";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("matId2", materialId));
        cmd.ExecuteNonQuery();
    }
}
