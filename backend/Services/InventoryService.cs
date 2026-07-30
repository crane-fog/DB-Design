using System.Data;

using Backend.Domain;
using Backend.Services.Interfaces;

using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>完工入库操作结果，供 IStockOperationService 使用。</summary>
public sealed record CompletionInboundResult(
    bool Ok,
    CompletionInboundOrder? Order,
    int ErrorCode,
    string? ErrorMessage)
{
    public static CompletionInboundResult Success(CompletionInboundOrder order) =>
        new(true, order, 200, null);
    public static CompletionInboundResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record InventoryAlertResult(
    bool Ok,
    InventoryAlertEvent? Alert,
    int ErrorCode,
    string? ErrorMessage)
{
    public static InventoryAlertResult Success(InventoryAlertEvent alert) =>
        new(true, alert, 200, null);
    public static InventoryAlertResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record LockStockResult(
    bool Ok,
    MaterialStockLockData? Data,
    int ErrorCode,
    string? ErrorMessage)
{
    public static LockStockResult Success(MaterialStockLockData data) =>
        new(true, data, 200, null);
    public static LockStockResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record StockLockResult(
    bool Ok,
    StockLockRecord? Record,
    int ErrorCode,
    string? ErrorMessage)
{
    public static StockLockResult Success(StockLockRecord record) =>
        new(true, record, 200, null);
    public static StockLockResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record ObsoleteHandleResult(
    bool Ok,
    ObsoleteMaterialDetection? Detection,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ObsoleteHandleResult Success(ObsoleteMaterialDetection detection) =>
        new(true, detection, 200, null);
    public static ObsoleteHandleResult Fail(int code, string message) =>
        new(false, null, code, message);
}

public sealed record ShortageResult(
    bool Ok,
    List<MaterialShortageItem>? Records,
    DateTime CalculationTime,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ShortageResult Success(List<MaterialShortageItem> records, DateTime time) =>
        new(true, records, time, 200, null);
    public static ShortageResult Fail(int code, string message) =>
        new(false, null, default, code, message);
}

/// <summary>
/// 库存管理主责 Service（B 模块）。维护 material_stock、stock_alert、stock_lock、
/// waste_detection、finish_inbound 表及供应商报价查询。
/// </summary>
public class InventoryService(string connString, IBomExpansionService? bomExpansion = null)
    : IPriceQueryService, IStockOperationService
{
    // ── material_stock ────────────────────────────────────────────
    private const string MaterialStockColumns = @"
        SELECT MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY, LAST_IN_DATE, LAST_OUT_DATE
        FROM MATERIAL_STOCK";

    // ── stock_alert ───────────────────────────────────────────────
    private const string AlertColumns = @"
        SELECT a.ALERT_ID, a.MATERIAL_ID, m.MATERIAL_NAME, a.ALERT_TYPE,
               a.AVAILABLE_QTY, a.THRESHOLD, a.ALERT_TIME, a.STATUS,
               a.HANDLER_ID, a.HANDLE_TIME
        FROM STOCK_ALERT a
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = a.MATERIAL_ID";

    // ── stock_lock ────────────────────────────────────────────────
    private const string LockColumns = @"
        SELECT l.LOCK_ID, l.ORDER_ID, l.MATERIAL_ID, m.MATERIAL_NAME,
               l.LOCK_QTY, l.LOCK_TIME, l.RELEASE_TIME, l.STATUS, l.OPERATOR_ID
        FROM STOCK_LOCK l
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = l.MATERIAL_ID";

    // ── waste_detection ───────────────────────────────────────────
    private const string DetectionColumns = @"
        SELECT d.DETECTION_ID, d.MATERIAL_ID, m.MATERIAL_NAME, d.DETECT_TIME,
               d.AVAILABLE_QTY, d.LAST_OUT_DATE, d.IDLE_DAYS, d.STATUS, d.HANDLER_ID
        FROM WASTE_DETECTION d
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = d.MATERIAL_ID";

    // ── finish_inbound ────────────────────────────────────────────
    private const string InboundColumns = @"
        SELECT i.INBOUND_ID, i.ORDER_ID, i.MATERIAL_ID, m.MATERIAL_NAME,
               i.VERSION_ID, i.FINISH_QTY, i.QUALIFIED_QTY, i.BATCH_NO,
               i.INBOUND_TIME, i.OPERATOR_ID
        FROM FINISH_INBOUND i
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = i.MATERIAL_ID";

    // ═══════════════════════════════════════════════════════════════
    //  getMaterialStockData（material_bom.yaml 中的 B 主责端点）
    // ═══════════════════════════════════════════════════════════════

    public MaterialStock? GetStockData(long materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = MaterialStockColumns + " WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapMaterialStock(reader) : null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Stock Alert 列表 / 生成 / 处理
    // ═══════════════════════════════════════════════════════════════

    public (List<InventoryAlertEvent> Records, int Total) ListAlerts(
        int page, int pageSize, long? materialId, string? dbStatus,
        DateTime? startTime, DateTime? endTime)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (materialId.HasValue) where.Add("a.MATERIAL_ID = :materialId");
        if (!string.IsNullOrEmpty(dbStatus)) where.Add("a.STATUS = :status");
        if (startTime.HasValue) where.Add("a.ALERT_TIME >= :startTime");
        if (endTime.HasValue) where.Add("a.ALERT_TIME <= :endTime");

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (materialId.HasValue) cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            if (!string.IsNullOrEmpty(dbStatus)) cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            if (startTime.HasValue) cmd.Parameters.Add(new OracleParameter("startTime", startTime.Value));
            if (endTime.HasValue) cmd.Parameters.Add(new OracleParameter("endTime", endTime.Value));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM STOCK_ALERT a" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<InventoryAlertEvent>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = AlertColumns + whereClause +
                @" ORDER BY a.ALERT_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapAlert(reader));
        }

        return (records, total);
    }

    public (int GeneratedCount, int SkippedPendingCount, List<InventoryAlertEvent> Records) GenerateAlerts(long? materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var generated = new List<InventoryAlertEvent>();
        int skipped = 0;

        string filterClause = materialId.HasValue ? "WHERE ms.MATERIAL_ID = :materialId" : "";
        using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = $@"
                SELECT ms.MATERIAL_ID, ms.AVAILABLE_QTY, m.SAFETY_STOCK
                FROM MATERIAL_STOCK ms
                JOIN MATERIAL m ON m.MATERIAL_ID = ms.MATERIAL_ID
                {filterClause}";
            if (materialId.HasValue)
                queryCmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));

            using var reader = queryCmd.ExecuteReader();
            var candidates = new List<(long MaterialId, decimal AvailableQty, decimal SafetyStock)>();
            while (reader.Read())
            {
                candidates.Add((Convert.ToInt64(reader.GetValue(0)), reader.GetDecimal(1), reader.GetDecimal(2)));
            }
            reader.Close();

            foreach (var (matId, availableQty, safetyStock) in candidates)
            {
                if (availableQty >= safetyStock) continue;

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM STOCK_ALERT WHERE MATERIAL_ID = :materialId AND STATUS = :pending";
                checkCmd.Parameters.Add(new OracleParameter("materialId", matId));
                checkCmd.Parameters.Add(new OracleParameter("pending", InventoryAlertStatusMap.Db.Pending));
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                {
                    skipped++;
                    continue;
                }

                long newId;
                var now = DateTime.Now;
                using (var insCmd = conn.CreateCommand())
                {
                    insCmd.CommandText = @"INSERT INTO STOCK_ALERT
                        (MATERIAL_ID, ALERT_TYPE, AVAILABLE_QTY, THRESHOLD, ALERT_TIME, STATUS)
                        VALUES ( :materialId, 'low_stock', :avail, :threshold, :alertTime, :status)
                        RETURNING ALERT_ID INTO :newId";
                    insCmd.Parameters.Add(new OracleParameter("materialId", matId));
                    insCmd.Parameters.Add(new OracleParameter("avail", availableQty));
                    insCmd.Parameters.Add(new OracleParameter("threshold", safetyStock));
                    insCmd.Parameters.Add(new OracleParameter("alertTime", now));
                    insCmd.Parameters.Add(new OracleParameter("status", InventoryAlertStatusMap.Db.Pending));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    insCmd.Parameters.Add(idParam);
                    insCmd.ExecuteNonQuery();
                    newId = Convert.ToInt64(idParam.Value.ToString());
                }

                generated.Add(MapAlertFromInsert(matId, availableQty, safetyStock, now, newId));
            }
        }

        return (generated.Count, skipped, generated);
    }

    public InventoryAlertResult HandleAlert(long alertId, string status, long handlerId)
    {
        if (status != "handled" && status != "ignored")
            return InventoryAlertResult.Fail(400, "状态必须为 handled 或 ignored");

        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawAlertStatus(conn, alertId);
        if (current is null) return InventoryAlertResult.Fail(404, "预警记录不存在");
        if (current != InventoryAlertStatusMap.Db.Pending)
            return InventoryAlertResult.Fail(409, "仅待处理预警可操作");

        var dbStatus = status == "handled"
            ? InventoryAlertStatusMap.Db.Handled
            : InventoryAlertStatusMap.Db.Ignored;

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE STOCK_ALERT
                SET STATUS = :status, HANDLER_ID = :handlerId, HANDLE_TIME = SYSDATE
                WHERE ALERT_ID = :alertId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            cmd.Parameters.Add(new OracleParameter("handlerId", handlerId));
            cmd.Parameters.Add(new OracleParameter("alertId", alertId));
            cmd.Parameters.Add(new OracleParameter("expected", InventoryAlertStatusMap.Db.Pending));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0) return InventoryAlertResult.Fail(409, "预警状态已变更，请刷新后重试");
        return InventoryAlertResult.Success(GetAlertInternal(conn, alertId)!);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Stock Lock 列表 / 锁定 / 释放
    // ═══════════════════════════════════════════════════════════════

    public (List<StockLockRecord> Records, int Total) ListLocks(
        int page, int pageSize, long? orderId, long? materialId, string? dbStatus)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (orderId.HasValue) where.Add("l.ORDER_ID = :orderId");
        if (materialId.HasValue) where.Add("l.MATERIAL_ID = :materialId");
        if (!string.IsNullOrEmpty(dbStatus)) where.Add("l.STATUS = :status");

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (orderId.HasValue) cmd.Parameters.Add(new OracleParameter("orderId", orderId.Value));
            if (materialId.HasValue) cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            if (!string.IsNullOrEmpty(dbStatus)) cmd.Parameters.Add(new OracleParameter("status", dbStatus));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM STOCK_LOCK l" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<StockLockRecord>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = LockColumns + whereClause +
                @" ORDER BY l.LOCK_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapLock(reader));
        }

        return (records, total);
    }

    public LockStockResult LockStock(long orderId, long operatorId,
        List<MaterialStockLockRequestItemsInner> items)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var lockRecords = new List<StockLockRecord>();
            var shortages = new List<MaterialStockLockDataShortagesInner>();

            foreach (var item in items)
            {
                if (item.LockQty <= 0)
                {
                    tx.Rollback();
                    return LockStockResult.Fail(400, $"物料 {item.MaterialId} 锁定数量必须大于 0");
                }

                decimal availableQty;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"SELECT AVAILABLE_QTY FROM MATERIAL_STOCK
                        WHERE MATERIAL_ID = :materialId FOR UPDATE";
                    cmd.Parameters.Add(new OracleParameter("materialId", item.MaterialId));
                    var result = cmd.ExecuteScalar();
                    availableQty = result is null or DBNull ? 0m : Convert.ToDecimal(result);
                }

                if (availableQty < item.LockQty)
                {
                    shortages.Add(new MaterialStockLockDataShortagesInner
                    {
                        MaterialId = item.MaterialId,
                        RequiredQty = item.LockQty,
                        AvailableQty = availableQty,
                        ShortageQty = item.LockQty - availableQty,
                    });
                    continue;
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE MATERIAL_STOCK
                        SET AVAILABLE_QTY = AVAILABLE_QTY - :qtyDeduct,
                            LOCKED_QTY = LOCKED_QTY + :qtyAdd,
                            LAST_OUT_DATE = SYSDATE
                        WHERE MATERIAL_ID = :materialId";
                    cmd.Parameters.Add(new OracleParameter("qtyDeduct", item.LockQty));
                    cmd.Parameters.Add(new OracleParameter("qtyAdd", item.LockQty));
                    cmd.Parameters.Add(new OracleParameter("materialId", item.MaterialId));
                    cmd.ExecuteNonQuery();
                }

                long lockId;
                var now = DateTime.Now;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO STOCK_LOCK
                        (ORDER_ID, MATERIAL_ID, LOCK_QTY, LOCK_TIME, STATUS, OPERATOR_ID)
                        VALUES (:orderId,  :materialId, :qty, :lockTime, :status, :operatorId)
                        RETURNING LOCK_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("orderId", orderId));
                    cmd.Parameters.Add(new OracleParameter("materialId", item.MaterialId));
                    cmd.Parameters.Add(new OracleParameter("qty", item.LockQty));
                    cmd.Parameters.Add(new OracleParameter("lockTime", now));
                    cmd.Parameters.Add(new OracleParameter("status", StockLockStatusMap.Db.Locked));
                    cmd.Parameters.Add(new OracleParameter("operatorId", operatorId));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    cmd.Parameters.Add(idParam);
                    cmd.ExecuteNonQuery();
                    lockId = Convert.ToInt64(idParam.Value.ToString());
                }

                lockRecords.Add(new StockLockRecord
                {
                    LockId = lockId,
                    OrderId = orderId,
                    MaterialId = item.MaterialId,
                    LockQty = item.LockQty,
                    Status = StockLockStatus.LockedEnum,
                    LockTime = now,
                    ReleaseTime = null,
                    OperatorId = operatorId,
                });
            }

            if (shortages.Count > 0)
            {
                tx.Rollback();
                return LockStockResult.Success(new MaterialStockLockData
                {
                    Success = false,
                    Records = lockRecords,
                    Shortages = shortages,
                });
            }

            tx.Commit();

            return LockStockResult.Success(new MaterialStockLockData
            {
                Success = true,
                Records = lockRecords,
                Shortages = new List<MaterialStockLockDataShortagesInner>(),
            });
        }
        catch (OracleException ex)
        {
            tx.Rollback();
            return LockStockResult.Fail(500, $"锁定库存失败: {ex.Message}");
        }
    }

    public StockLockResult ReleaseStock(long lockId, long operatorId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawLockStatus(conn, lockId);
        if (current is null) return StockLockResult.Fail(404, "锁定记录不存在");
        if (current != StockLockStatusMap.Db.Locked)
            return StockLockResult.Fail(409, "仅已锁定记录可释放");

        long materialId;
        decimal lockQty;
        using (var getCmd = conn.CreateCommand())
        {
            getCmd.CommandText = "SELECT MATERIAL_ID, LOCK_QTY FROM STOCK_LOCK WHERE LOCK_ID = :lockId";
            getCmd.Parameters.Add(new OracleParameter("lockId", lockId));
            using var r = getCmd.ExecuteReader();
            r.Read();
            materialId = Convert.ToInt64(r.GetValue(0));
            lockQty = r.GetDecimal(1);
        }

        using var tx = conn.BeginTransaction();
        try
        {
            int affected;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE STOCK_LOCK
                    SET STATUS = :newStatus, RELEASE_TIME = :releaseTime
                    WHERE LOCK_ID = :lockId AND STATUS = :expected";
                cmd.Parameters.Add(new OracleParameter("newStatus", StockLockStatusMap.Db.Cancelled));
                cmd.Parameters.Add(new OracleParameter("releaseTime", DateTime.Now));
                cmd.Parameters.Add(new OracleParameter("lockId", lockId));
                cmd.Parameters.Add(new OracleParameter("expected", StockLockStatusMap.Db.Locked));
                affected = cmd.ExecuteNonQuery();
            }

            if (affected == 0)
            {
                tx.Rollback();
                return StockLockResult.Fail(409, "锁定状态已变更，请刷新后重试");
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE MATERIAL_STOCK
                    SET AVAILABLE_QTY = AVAILABLE_QTY + :qtyToAdd,
                        LOCKED_QTY = LOCKED_QTY - :qtyToSub
                    WHERE MATERIAL_ID = :materialId";
                cmd.Parameters.Add(new OracleParameter("qtyToAdd", lockQty));
                cmd.Parameters.Add(new OracleParameter("qtyToSub", lockQty));
                cmd.Parameters.Add(new OracleParameter("materialId", materialId));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return StockLockResult.Success(GetLockInternal(conn, lockId)!);
        }
        catch (OracleException ex)
        {
            tx.Rollback();
            return StockLockResult.Fail(500, $"释放库存失败: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Obsolete Material Detection 列表 / 检测 / 处理
    // ═══════════════════════════════════════════════════════════════

    public (List<ObsoleteMaterialDetection> Records, int Total) ListDetections(
        int page, int pageSize, long? materialId, string? dbStatus,
        DateTime? detectStart, DateTime? detectEnd)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (materialId.HasValue) where.Add("d.MATERIAL_ID = :materialId");
        if (!string.IsNullOrEmpty(dbStatus)) where.Add("d.STATUS = :status");
        if (detectStart.HasValue) where.Add("d.DETECT_TIME >= :detectStart");
        if (detectEnd.HasValue) where.Add("d.DETECT_TIME <= :detectEnd");

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (materialId.HasValue) cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            if (!string.IsNullOrEmpty(dbStatus)) cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            if (detectStart.HasValue) cmd.Parameters.Add(new OracleParameter("detectStart", detectStart.Value));
            if (detectEnd.HasValue) cmd.Parameters.Add(new OracleParameter("detectEnd", detectEnd.Value));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM WASTE_DETECTION d" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<ObsoleteMaterialDetection>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = DetectionColumns + whereClause +
                @" ORDER BY d.DETECTION_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapDetection(reader));
        }

        return (records, total);
    }

    public (int DetectedCount, List<ObsoleteMaterialDetection> Records) DetectObsolete(
        int idleDaysThreshold, long? materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var detections = new List<ObsoleteMaterialDetection>();
        var now = DateTime.Now;
        var cutoff = now.AddDays(-idleDaysThreshold);

        string matFilter = materialId.HasValue ? "AND ms.MATERIAL_ID = :materialId" : "";

        using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = $@"
                SELECT ms.MATERIAL_ID, ms.AVAILABLE_QTY, ms.LAST_OUT_DATE
                FROM MATERIAL_STOCK ms
                WHERE ms.LAST_OUT_DATE IS NOT NULL
                  AND ms.LAST_OUT_DATE <= :cutoff
                  AND ms.AVAILABLE_QTY > 0
                  {matFilter}";
            queryCmd.Parameters.Add(new OracleParameter("cutoff", cutoff));
            if (materialId.HasValue)
                queryCmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));

            using var reader = queryCmd.ExecuteReader();
            var candidates = new List<(long MaterialId, decimal AvailableQty, DateTime? LastOutDate)>();
            while (reader.Read())
            {
                candidates.Add((
                    Convert.ToInt64(reader.GetValue(0)),
                    reader.GetDecimal(1),
                    reader.IsDBNull(2) ? null : reader.GetDateTime(2)));
            }
            reader.Close();

            foreach (var (matId, availableQty, lastOutDate) in candidates)
            {
                if (!IsMaterialActive(conn, matId)) continue;

                int idleDays = lastOutDate.HasValue
                    ? (int)(now - lastOutDate.Value).TotalDays
                    : 0;

                long newId;
                using (var insCmd = conn.CreateCommand())
                {
                    insCmd.CommandText = @"INSERT INTO WASTE_DETECTION
                        (MATERIAL_ID, DETECT_TIME, AVAILABLE_QTY, LAST_OUT_DATE, IDLE_DAYS, STATUS)
                        VALUES ( :materialId, :dt, :avail, :lastOut, :idleDays, :status)
                        RETURNING DETECTION_ID INTO :newId";
                    insCmd.Parameters.Add(new OracleParameter("materialId", matId));
                    insCmd.Parameters.Add(new OracleParameter("dt", now));
                    insCmd.Parameters.Add(new OracleParameter("avail", availableQty));
                    insCmd.Parameters.Add(new OracleParameter("lastOut",
                        lastOutDate.HasValue ? lastOutDate.Value : DBNull.Value));
                    insCmd.Parameters.Add(new OracleParameter("idleDays", idleDays));
                    insCmd.Parameters.Add(new OracleParameter("status", ObsoleteMaterialStatusMap.Db.Pending));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    insCmd.Parameters.Add(idParam);
                    insCmd.ExecuteNonQuery();
                    newId = Convert.ToInt64(idParam.Value.ToString());
                }

                detections.Add(new ObsoleteMaterialDetection
                {
                    DetectionId = newId,
                    MaterialId = matId,
                    DetectTime = now,
                    AvailableQty = availableQty,
                    LastOutDate = lastOutDate.HasValue
                        ? DateOnly.FromDateTime(lastOutDate.Value) : null,
                    IdleDays = idleDays,
                    Status = ObsoleteMaterialStatus.PendingEnum,
                    HandlerId = null,
                });
            }
        }

        return (detections.Count, detections);
    }

    public ObsoleteHandleResult HandleDetection(long detectionId, string status, long handlerId)
    {
        if (status != "handled" && status != "ignored")
            return ObsoleteHandleResult.Fail(400, "状态必须为 handled 或 ignored");

        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawDetectionStatus(conn, detectionId);
        if (current is null) return ObsoleteHandleResult.Fail(404, "检测记录不存在");
        if (current != ObsoleteMaterialStatusMap.Db.Pending)
            return ObsoleteHandleResult.Fail(409, "仅待处理检测可操作");

        var dbStatus = status == "handled"
            ? ObsoleteMaterialStatusMap.Db.Handled
            : ObsoleteMaterialStatusMap.Db.Ignored;

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE WASTE_DETECTION
                SET STATUS = :status, HANDLER_ID = :handlerId
                WHERE DETECTION_ID = :detectionId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            cmd.Parameters.Add(new OracleParameter("handlerId", handlerId));
            cmd.Parameters.Add(new OracleParameter("detectionId", detectionId));
            cmd.Parameters.Add(new OracleParameter("expected", ObsoleteMaterialStatusMap.Db.Pending));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0) return ObsoleteHandleResult.Fail(409, "检测状态已变更，请刷新后重试");
        return ObsoleteHandleResult.Success(GetDetectionInternal(conn, detectionId)!);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Completion Inbound 列表 / 入库
    // ═══════════════════════════════════════════════════════════════

    public (List<CompletionInboundOrder> Records, int Total) ListInbound(
        int page, int pageSize, long? orderId, long? materialId,
        DateTime? inboundStart, DateTime? inboundEnd)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (orderId.HasValue) where.Add("i.ORDER_ID = :orderId");
        if (materialId.HasValue) where.Add("i.MATERIAL_ID = :materialId");
        if (inboundStart.HasValue) where.Add("i.INBOUND_TIME >= :inboundStart");
        if (inboundEnd.HasValue) where.Add("i.INBOUND_TIME <= :inboundEnd");

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

        void AddFilters(OracleCommand cmd)
        {
            if (orderId.HasValue) cmd.Parameters.Add(new OracleParameter("orderId", orderId.Value));
            if (materialId.HasValue) cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            if (inboundStart.HasValue) cmd.Parameters.Add(new OracleParameter("inboundStart", inboundStart.Value));
            if (inboundEnd.HasValue) cmd.Parameters.Add(new OracleParameter("inboundEnd", inboundEnd.Value));
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM FINISH_INBOUND i" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<CompletionInboundOrder>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = InboundColumns + whereClause +
                @" ORDER BY i.INBOUND_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) records.Add(MapInbound(reader, conn));
        }

        return (records, total);
    }

    public CompletionInboundResult AddInbound(CompletionInboundCreateRequest request)
    {
        if (request.FinishQty <= 0)
            return CompletionInboundResult.Fail(400, "完工数量必须大于 0");
        if (request.QualifiedQty > request.FinishQty)
            return CompletionInboundResult.Fail(400, "合格数量不得大于完工数量");

        using var conn = new OracleConnection(connString);
        conn.Open();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"SELECT COUNT(*) FROM FINISH_INBOUND
            WHERE ORDER_ID = :orderId AND MATERIAL_ID = :materialId";
        checkCmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
        checkCmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
        if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
            return CompletionInboundResult.Fail(409, "该订单已存在入库记录");

        var consumedLocks = new List<StockLockRecord>();
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                long inboundId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO FINISH_INBOUND
                        (ORDER_ID, MATERIAL_ID, VERSION_ID, FINISH_QTY, QUALIFIED_QTY, BATCH_NO, INBOUND_TIME, OPERATOR_ID)
                        VALUES (:orderId,  :materialId, :versionId, :finishQty, :qualifiedQty, :batchNo, SYSDATE, :operatorId)
                        RETURNING INBOUND_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
                    cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
                    cmd.Parameters.Add(new OracleParameter("versionId", request.VersionId));
                    cmd.Parameters.Add(new OracleParameter("finishQty", request.FinishQty));
                    cmd.Parameters.Add(new OracleParameter("qualifiedQty", request.QualifiedQty));
                    cmd.Parameters.Add(new OracleParameter("batchNo", request.BatchNo ?? (object)DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("operatorId", request.OperatorId));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    cmd.Parameters.Add(idParam);
                    cmd.ExecuteNonQuery();
                    inboundId = Convert.ToInt64(idParam.Value.ToString());
                }

                var lockQuantities = new Dictionary<long, decimal>();
                using (var lockCmd = conn.CreateCommand())
                {
                    lockCmd.Transaction = tx;
                    lockCmd.CommandText = @"SELECT LOCK_ID, MATERIAL_ID, LOCK_QTY
                        FROM STOCK_LOCK
                        WHERE ORDER_ID = :orderId AND STATUS = :expected";
                    lockCmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
                    lockCmd.Parameters.Add(new OracleParameter("expected", StockLockStatusMap.Db.Locked));
                    using var lr = lockCmd.ExecuteReader();
                    while (lr.Read())
                    {
                        long lockId = Convert.ToInt64(lr.GetValue(0));
                        long lockMatId = Convert.ToInt64(lr.GetValue(1));
                        decimal lockQty = lr.GetDecimal(2);
                        consumedLocks.Add(new StockLockRecord
                        {
                            LockId = lockId,
                            OrderId = request.OrderId,
                            MaterialId = lockMatId,
                            LockQty = lockQty,
                            Status = StockLockStatus.ConsumedEnum,
                            LockTime = DateTime.MinValue,
                            ReleaseTime = DateTime.Now,
                            OperatorId = request.OperatorId,
                        });

                        if (!lockQuantities.ContainsKey(lockMatId))
                            lockQuantities[lockMatId] = 0;
                        lockQuantities[lockMatId] += lockQty;
                    }
                }

                using (var updCmd = conn.CreateCommand())
                {
                    updCmd.Transaction = tx;
                    updCmd.CommandText = @"UPDATE STOCK_LOCK
                        SET STATUS = :newStatus, RELEASE_TIME = SYSDATE
                        WHERE ORDER_ID = :orderId AND STATUS = :expected";
                    updCmd.Parameters.Add(new OracleParameter("newStatus", StockLockStatusMap.Db.Consumed));
                    updCmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
                    updCmd.Parameters.Add(new OracleParameter("expected", StockLockStatusMap.Db.Locked));
                    updCmd.ExecuteNonQuery();
                }

                foreach (var (materialId, qty) in lockQuantities)
                {
                    using var stockCmd = conn.CreateCommand();
                    stockCmd.Transaction = tx;
                    stockCmd.CommandText = @"UPDATE MATERIAL_STOCK
                        SET LOCKED_QTY = LOCKED_QTY - :lockQtyDeduct
                        WHERE MATERIAL_ID = :materialId";
                    stockCmd.Parameters.Add(new OracleParameter("lockQtyDeduct", qty));
                    stockCmd.Parameters.Add(new OracleParameter("materialId", materialId));
                    stockCmd.ExecuteNonQuery();
                }

                using (var finCmd = conn.CreateCommand())
                {
                    finCmd.Transaction = tx;
                    finCmd.CommandText = @"MERGE INTO MATERIAL_STOCK ms
                        USING (SELECT :materialId AS MAT_ID FROM DUAL) d
                        ON (ms.MATERIAL_ID = d.MAT_ID)
                        WHEN MATCHED THEN
                            UPDATE SET AVAILABLE_QTY = AVAILABLE_QTY + :qualifiedQty,
                                       LAST_IN_DATE = SYSDATE
                        WHEN NOT MATCHED THEN
                            INSERT (MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY, LAST_IN_DATE)
                            VALUES (:matId2, :qualifiedQty2, 0, SYSDATE)";
                    finCmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
                    finCmd.Parameters.Add(new OracleParameter("qualifiedQty", request.QualifiedQty));
                    finCmd.Parameters.Add(new OracleParameter("matId2", request.MaterialId));
                    finCmd.Parameters.Add(new OracleParameter("qualifiedQty2", request.QualifiedQty));
                    finCmd.ExecuteNonQuery();
                }

                tx.Commit();

                var order = GetInboundInternal(conn, inboundId)!;
                order.ConsumedLockRecords = consumedLocks;
                return CompletionInboundResult.Success(order);
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                return CompletionInboundResult.Fail(500, $"入库失败: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Calculate Material Shortage 物料缺口计算
    // ═══════════════════════════════════════════════════════════════

    public ShortageResult CalculateShortage(MaterialShortageCalculateRequest request)
    {
        if (bomExpansion is null)
            return ShortageResult.Fail(500, "BOM 展开服务尚未接入（等待 A 模块实现 IBomExpansionService）");

        using var conn = new OracleConnection(connString);
        conn.Open();

        var allRecords = new List<MaterialShortageItem>();
        var calcTime = DateTime.Now;

        foreach (var reqItem in request.Items)
        {
            var nodes = bomExpansion.Expand(reqItem.MaterialId, reqItem.VersionId);
            foreach (var node in nodes)
            {
                decimal grossRequirement =
                    reqItem.ProductionQty * node.Quantity / (1 - node.LossRate);
                grossRequirement = Math.Ceiling(grossRequirement * 100) / 100;

                decimal availableQty = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT AVAILABLE_QTY FROM MATERIAL_STOCK WHERE MATERIAL_ID = :materialId";
                    cmd.Parameters.Add(new OracleParameter("materialId", node.MaterialId));
                    var r = cmd.ExecuteScalar();
                    if (r is not null and not DBNull) availableQty = Convert.ToDecimal(r);
                }

                decimal inTransitQty = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COALESCE(SUM(poi.QUANTITY - NVL(poi.RECEIVED_QTY, 0)), 0)
                        FROM PURCHASE_ORDER_ITEM poi
                        JOIN PURCHASE_ORDER po ON po.ORDER_ID = poi.ORDER_ID
                        WHERE poi.MATERIAL_ID =  :materialId
                          AND po.STATUS IN (:submitted, :partial)";
                    cmd.Parameters.Add(new OracleParameter("materialId", node.MaterialId));
                    cmd.Parameters.Add(new OracleParameter("submitted", PurchaseOrderStatusMap.Db.Submitted));
                    cmd.Parameters.Add(new OracleParameter("partial", PurchaseOrderStatusMap.Db.PartialReceived));
                    inTransitQty = Convert.ToDecimal(cmd.ExecuteScalar());
                }

                decimal safetyStock = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SAFETY_STOCK FROM MATERIAL WHERE MATERIAL_ID = :materialId";
                    cmd.Parameters.Add(new OracleParameter("materialId", node.MaterialId));
                    var r = cmd.ExecuteScalar();
                    if (r is not null and not DBNull) safetyStock = Convert.ToDecimal(r);
                }

                decimal netShortage = Math.Max(
                    grossRequirement - availableQty - inTransitQty + safetyStock, 0);
                netShortage = Math.Ceiling(netShortage * 100) / 100;

                string? materialName = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MATERIAL_NAME FROM MATERIAL WHERE MATERIAL_ID = :materialId";
                    cmd.Parameters.Add(new OracleParameter("materialId", node.MaterialId));
                    var r = cmd.ExecuteScalar();
                    if (r is not null and not DBNull) materialName = r.ToString();
                }

                allRecords.Add(new MaterialShortageItem
                {
                    MaterialId = node.MaterialId,
                    MaterialName = materialName ?? string.Empty,
                    ParentMaterialId = node.ParentMaterialId,
                    Level = node.Level,
                    GrossRequirement = grossRequirement,
                    AvailableQty = availableQty,
                    InTransitQty = inTransitQty,
                    SafetyStock = safetyStock,
                    NetShortageQty = netShortage,
                    SuggestedPurchaseQty = netShortage,
                });
            }
        }

        return ShortageResult.Success(allRecords, calcTime);
    }

    // ═══════════════════════════════════════════════════════════════
    //  IPriceQueryService 实现
    // ═══════════════════════════════════════════════════════════════

    public decimal? GetCurrentPrice(long materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT PRICE FROM SUPPLIER_PRICE
            WHERE MATERIAL_ID =  :materialId
              AND VALID_FROM <= SYSDATE
              AND (VALID_TO IS NULL OR VALID_TO >= SYSDATE)
            ORDER BY VALID_FROM DESC
            FETCH FIRST 1 ROW ONLY";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToDecimal(result);
    }

    public Dictionary<long, decimal?> GetCurrentPrices(IEnumerable<long> materialIds)
    {
        var result = new Dictionary<long, decimal?>();
        foreach (var id in materialIds)
            result[id] = GetCurrentPrice(id);
        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  IStockOperationService 实现（供 C 模块调用）
    // ═══════════════════════════════════════════════════════════════

    public CompletionInboundResult RecordFinishInbound(
        long orderId, long materialId, long versionId,
        decimal finishQty, decimal qualifiedQty, string batchNo, long operatorId)
    {
        return AddInbound(new CompletionInboundCreateRequest
        {
            OrderId = orderId,
            MaterialId = materialId,
            VersionId = versionId,
            FinishQty = finishQty,
            QualifiedQty = qualifiedQty,
            BatchNo = batchNo,
            OperatorId = operatorId,
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════════════════════

    private static MaterialStock MapMaterialStock(OracleDataReader reader) => new()
    {
        MaterialId = Convert.ToInt32(reader.GetValue(0)),
        AvailableQty = Convert.ToDouble(reader.GetValue(1)),
        LockedQty = Convert.ToDouble(reader.GetValue(2)),
        LastInDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
        LastOutDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
    };

    private static InventoryAlertEvent MapAlert(OracleDataReader reader) => new()
    {
        AlertId = Convert.ToInt64(reader.GetValue(0)),
        MaterialId = Convert.ToInt64(reader.GetValue(1)),
        MaterialName = reader.IsDBNull(2) ? null! : reader.GetString(2),
        AlertType = InventoryAlertEvent.AlertTypeEnum.LowStockEnum,
        AvailableQty = reader.GetDecimal(4),
        Threshold = reader.GetDecimal(5),
        AlertTime = reader.GetDateTime(6),
        Status = InventoryAlertStatusMap.FromDb(reader.GetString(7)),
        HandlerId = reader.IsDBNull(8) ? null : Convert.ToInt64(reader.GetValue(8)),
        HandleTime = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
    };

    private static InventoryAlertEvent MapAlertFromInsert(
        long materialId, decimal availableQty,
        decimal safetyStock, DateTime now, long alertId)
    {
        return new InventoryAlertEvent
        {
            AlertId = alertId,
            MaterialId = materialId,
            AvailableQty = availableQty,
            Threshold = safetyStock,
            AlertTime = now,
            Status = InventoryAlertStatus.PendingEnum,
        };
    }

    private static StockLockRecord MapLock(OracleDataReader reader) => new()
    {
        LockId = Convert.ToInt64(reader.GetValue(0)),
        OrderId = Convert.ToInt64(reader.GetValue(1)),
        MaterialId = Convert.ToInt64(reader.GetValue(2)),
        MaterialName = reader.IsDBNull(3) ? null! : reader.GetString(3),
        LockQty = reader.GetDecimal(4),
        LockTime = reader.GetDateTime(5),
        ReleaseTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
        Status = StockLockStatusMap.FromDb(reader.GetString(7)),
        OperatorId = Convert.ToInt64(reader.GetValue(8)),
    };

    private static ObsoleteMaterialDetection MapDetection(OracleDataReader reader) => new()
    {
        DetectionId = Convert.ToInt64(reader.GetValue(0)),
        MaterialId = Convert.ToInt64(reader.GetValue(1)),
        MaterialName = reader.IsDBNull(2) ? null! : reader.GetString(2),
        DetectTime = reader.GetDateTime(3),
        AvailableQty = reader.GetDecimal(4),
        LastOutDate = reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5)),
        IdleDays = Convert.ToInt32(reader.GetValue(6)),
        Status = ObsoleteMaterialStatusMap.FromDb(reader.GetString(7)),
        HandlerId = reader.IsDBNull(8) ? null : Convert.ToInt64(reader.GetValue(8)),
    };

    private static CompletionInboundOrder MapInbound(OracleDataReader reader, OracleConnection conn)
    {
        var orderId = Convert.ToInt64(reader.GetValue(1));
        var consumedLocks = GetConsumedLocks(conn, orderId);

        return new CompletionInboundOrder
        {
            InboundId = Convert.ToInt64(reader.GetValue(0)),
            OrderId = orderId,
            MaterialId = Convert.ToInt64(reader.GetValue(2)),
            ProductName = reader.IsDBNull(3) ? null! : reader.GetString(3),
            VersionId = Convert.ToInt64(reader.GetValue(4)),
            FinishQty = reader.GetDecimal(5),
            QualifiedQty = reader.GetDecimal(6),
            BatchNo = reader.IsDBNull(7) ? null! : reader.GetString(7),
            InboundTime = reader.GetDateTime(8),
            OperatorId = Convert.ToInt64(reader.GetValue(9)),
            ConsumedLockRecords = consumedLocks,
        };
    }

    private static List<StockLockRecord> GetConsumedLocks(OracleConnection conn, long orderId)
    {
        var locks = new List<StockLockRecord>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = LockColumns + " WHERE l.ORDER_ID = :orderId AND l.STATUS = :status ORDER BY l.LOCK_ID";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        cmd.Parameters.Add(new OracleParameter("status", StockLockStatusMap.Db.Consumed));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            locks.Add(new StockLockRecord
            {
                LockId = Convert.ToInt64(reader.GetValue(0)),
                OrderId = Convert.ToInt64(reader.GetValue(1)),
                MaterialId = Convert.ToInt64(reader.GetValue(2)),
                MaterialName = reader.IsDBNull(3) ? null! : reader.GetString(3),
                LockQty = reader.GetDecimal(4),
                LockTime = reader.GetDateTime(5),
                ReleaseTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                Status = StockLockStatusMap.FromDb(reader.GetString(7)),
                OperatorId = Convert.ToInt64(reader.GetValue(8)),
            });
        }
        return locks;
    }

    private static InventoryAlertEvent? GetAlertInternal(OracleConnection conn, long alertId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = AlertColumns + " WHERE a.ALERT_ID = :alertId";
        cmd.Parameters.Add(new OracleParameter("alertId", alertId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapAlert(reader) : null;
    }

    private static StockLockRecord? GetLockInternal(OracleConnection conn, long lockId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = LockColumns + " WHERE l.LOCK_ID = :lockId";
        cmd.Parameters.Add(new OracleParameter("lockId", lockId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapLock(reader) : null;
    }

    private static ObsoleteMaterialDetection? GetDetectionInternal(OracleConnection conn, long detectionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = DetectionColumns + " WHERE d.DETECTION_ID = :detectionId";
        cmd.Parameters.Add(new OracleParameter("detectionId", detectionId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapDetection(reader) : null;
    }

    private static CompletionInboundOrder? GetInboundInternal(OracleConnection conn, long inboundId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = InboundColumns + " WHERE i.INBOUND_ID = :inboundId";
        cmd.Parameters.Add(new OracleParameter("inboundId", inboundId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapInbound(reader, conn) : null;
    }

    private static string? GetRawAlertStatus(OracleConnection conn, long alertId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM STOCK_ALERT WHERE ALERT_ID = :alertId";
        cmd.Parameters.Add(new OracleParameter("alertId", alertId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static string? GetRawLockStatus(OracleConnection conn, long lockId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM STOCK_LOCK WHERE LOCK_ID = :lockId";
        cmd.Parameters.Add(new OracleParameter("lockId", lockId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static string? GetRawDetectionStatus(OracleConnection conn, long detectionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM WASTE_DETECTION WHERE DETECTION_ID = :detectionId";
        cmd.Parameters.Add(new OracleParameter("detectionId", detectionId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    /// <summary>
    /// 检查物料是否仍在有效 BOM 版本中被引用且有关联的活跃生产订单。
    /// 返回 true 表示物料仍在使用中，不应标记为废弃。
    /// </summary>
    private static bool IsMaterialActive(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM BOM b
            JOIN BOM_VERSION bv ON bv.VERSION_ID = b.VERSION_ID
            JOIN PRODUCTION_ORDER po ON po.VERSION_ID = bv.VERSION_ID
            WHERE b.CHILD_MATERIAL_ID =  :materialId
              AND bv.EXPIRE_DATE IS NULL
              AND po.STATUS IN (:inProgress, :pendingSchedule, :pendingReview)";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("inProgress", ProductionStatusMap.Db.InProgress));
        cmd.Parameters.Add(new OracleParameter("pendingSchedule", ProductionStatusMap.Db.PendingSchedule));
        cmd.Parameters.Add(new OracleParameter("pendingReview", ProductionStatusMap.Db.PendingReview));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
