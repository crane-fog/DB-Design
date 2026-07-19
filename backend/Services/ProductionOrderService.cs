using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>生产订单业务操作结果，Ok=false 时 ErrorCode 指示业务码（400/404/409）。</summary>
public sealed record ProductionOrderResult(
    bool Ok,
    ProductionOrderDetail? Order,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ProductionOrderResult Success(ProductionOrderDetail order) =>
        new(true, order, 200, null);

    public static ProductionOrderResult Fail(int code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// 生产订单主责 Service（C 模块）。维护 production_order 表及其状态机：
/// pending_review → pending_schedule → in_progress → completed，任意非终态可 → cancelled。
/// 展示用的 material_name / version_no 通过 JOIN material、bom_version 得到，不维护这些表。
/// 按当前分工，本阶段完工不联动库存（stock_lock / finish_inbound / material_stock 待 B 就绪后接入）。
/// </summary>
public class ProductionOrderService(string connString)
{
    // 注：production_order 表无 review_comment 列（仅 external_order 有），
    // 契约中的 ProductionOrderDetail.review_comment 无对应存储列，故查询不选取、响应恒为 null。
    private const string SelectColumns = @"
        SELECT po.ORDER_ID, po.MATERIAL_ID, m.MATERIAL_NAME, po.PLAN_QTY, po.FINISHED_QTY,
               po.STATUS, po.VERSION_ID, bv.VERSION_NO, po.PLAN_START, po.PLAN_END,
               po.ACTUAL_START, po.ACTUAL_END
        FROM PRODUCTION_ORDER po
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = po.MATERIAL_ID
        LEFT JOIN BOM_VERSION bv ON bv.VERSION_ID = po.VERSION_ID";

    public (List<ProductionOrderDetail> Records, int Total) List(
        int page,
        int pageSize,
        long? materialId,
        string? dbStatus,
        DateOnly? planEndStart,
        DateOnly? planEndEnd)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (materialId.HasValue)
        {
            where.Add("po.MATERIAL_ID = :materialId");
        }

        if (!string.IsNullOrEmpty(dbStatus))
        {
            where.Add("po.STATUS = :status");
        }

        if (planEndStart.HasValue)
        {
            where.Add("po.PLAN_END >= :planEndStart");
        }

        if (planEndEnd.HasValue)
        {
            where.Add("po.PLAN_END <= :planEndEnd");
        }

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;

        void AddFilters(OracleCommand cmd)
        {
            if (materialId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            }

            if (!string.IsNullOrEmpty(dbStatus))
            {
                cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            }

            if (planEndStart.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("planEndStart", planEndStart.Value.ToDateTime(TimeOnly.MinValue)));
            }

            if (planEndEnd.HasValue)
            {
                // 用当天最大时间作为上界，避免排除 plan_end 落在当天较晚时刻的记录。
                cmd.Parameters.Add(new OracleParameter("planEndEnd", planEndEnd.Value.ToDateTime(TimeOnly.MaxValue)));
            }
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM PRODUCTION_ORDER po" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<ProductionOrderDetail>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SelectColumns + whereClause +
                @" ORDER BY po.ORDER_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapDetail(reader));
            }
        }

        return (records, total);
    }

    public ProductionOrderDetail? Get(long orderId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        return GetInternal(conn, orderId);
    }

    public ProductionOrderResult Create(ProductionOrderCreateRequest request)
    {
        if (request.PlanQty <= 0)
        {
            return ProductionOrderResult.Fail(400, "计划数量必须大于 0");
        }

        if (request.PlanEnd < request.PlanStart)
        {
            return ProductionOrderResult.Fail(400, "计划完工日期不得早于计划开始日期");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (!MaterialExists(conn, request.MaterialId))
        {
            return ProductionOrderResult.Fail(400, "产品不存在");
        }

        if (!BomVersionExists(conn, request.VersionId, request.MaterialId))
        {
            return ProductionOrderResult.Fail(400, "BOM 版本不存在或与产品不匹配");
        }

        long newId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO PRODUCTION_ORDER
                                (MATERIAL_ID, VERSION_ID, PLAN_QTY, FINISHED_QTY, PLAN_START, PLAN_END, STATUS)
                                VALUES (:materialId, :versionId, :planQty, 0, :planStart, :planEnd, :status)
                                RETURNING ORDER_ID INTO :newId";
            cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
            cmd.Parameters.Add(new OracleParameter("versionId", request.VersionId));
            cmd.Parameters.Add(new OracleParameter("planQty", request.PlanQty));
            cmd.Parameters.Add(new OracleParameter("planStart", request.PlanStart.ToDateTime(TimeOnly.MinValue)));
            cmd.Parameters.Add(new OracleParameter("planEnd", request.PlanEnd.ToDateTime(TimeOnly.MinValue)));
            cmd.Parameters.Add(new OracleParameter("status", ProductionStatusMap.Db.PendingReview));
            var idParam = new OracleParameter("newId", OracleDbType.Int64)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(idParam);
            cmd.ExecuteNonQuery();
            newId = Convert.ToInt64(idParam.Value.ToString());
        }

        return ProductionOrderResult.Success(GetInternal(conn, newId)!);
    }

    public ProductionOrderResult Update(ProductionOrderUpdateRequest request)
    {
        if (request.PlanQty <= 0)
        {
            return ProductionOrderResult.Fail(400, "计划数量必须大于 0");
        }

        if (request.PlanEnd < request.PlanStart)
        {
            return ProductionOrderResult.Fail(400, "计划完工日期不得早于计划开始日期");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.OrderId);
        if (current is null)
        {
            return ProductionOrderResult.Fail(404, "生产订单不存在");
        }

        if (current is not (ProductionStatusMap.Db.PendingReview or ProductionStatusMap.Db.PendingSchedule))
        {
            return ProductionOrderResult.Fail(409, "当前状态不允许修改");
        }

        if (!MaterialExists(conn, request.MaterialId))
        {
            return ProductionOrderResult.Fail(400, "产品不存在");
        }

        if (!BomVersionExists(conn, request.VersionId, request.MaterialId))
        {
            return ProductionOrderResult.Fail(400, "BOM 版本不存在或与产品不匹配");
        }

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PRODUCTION_ORDER
                                SET MATERIAL_ID = :materialId, VERSION_ID = :versionId,
                                    PLAN_QTY = :planQty, PLAN_START = :planStart, PLAN_END = :planEnd
                                WHERE ORDER_ID = :orderId
                                  AND STATUS IN (:allowedReview, :allowedSchedule)";
            cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
            cmd.Parameters.Add(new OracleParameter("versionId", request.VersionId));
            cmd.Parameters.Add(new OracleParameter("planQty", request.PlanQty));
            cmd.Parameters.Add(new OracleParameter("planStart", request.PlanStart.ToDateTime(TimeOnly.MinValue)));
            cmd.Parameters.Add(new OracleParameter("planEnd", request.PlanEnd.ToDateTime(TimeOnly.MinValue)));
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("allowedReview", ProductionStatusMap.Db.PendingReview));
            cmd.Parameters.Add(new OracleParameter("allowedSchedule", ProductionStatusMap.Db.PendingSchedule));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
        {
            return ProductionOrderResult.Fail(409, "订单状态已变更，请刷新后重试");
        }

        return ProductionOrderResult.Success(GetInternal(conn, request.OrderId)!);
    }

    public ProductionOrderResult Approve(ProductionOrderApproveRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.OrderId);
        if (current is null)
        {
            return ProductionOrderResult.Fail(404, "生产订单不存在");
        }

        if (current != ProductionStatusMap.Db.PendingReview)
        {
            return ProductionOrderResult.Fail(409, "仅待审核订单可审核");
        }

        var newStatus = request.Approved ? ProductionStatusMap.Db.PendingSchedule : ProductionStatusMap.Db.Cancelled;

        // production_order 无 review_comment 列，审核意见暂不落库（仅驱动状态流转）。
        // 状态作为 UPDATE 条件的一部分，确保并发下只有一次流转生效，避免绕过状态机。
        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PRODUCTION_ORDER
                                SET STATUS = :status
                                WHERE ORDER_ID = :orderId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", newStatus));
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("expected", ProductionStatusMap.Db.PendingReview));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
        {
            return ProductionOrderResult.Fail(409, "订单状态已变更，请刷新后重试");
        }

        return ProductionOrderResult.Success(GetInternal(conn, request.OrderId)!);
    }

    public ProductionOrderResult Start(ProductionOrderActionRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.OrderId);
        if (current is null)
        {
            return ProductionOrderResult.Fail(404, "生产订单不存在");
        }

        if (current != ProductionStatusMap.Db.PendingSchedule)
        {
            return ProductionOrderResult.Fail(409, "仅待排产订单可开工");
        }

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PRODUCTION_ORDER
                                SET STATUS = :status, ACTUAL_START = SYSDATE
                                WHERE ORDER_ID = :orderId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", ProductionStatusMap.Db.InProgress));
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("expected", ProductionStatusMap.Db.PendingSchedule));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
        {
            return ProductionOrderResult.Fail(409, "订单状态已变更，请刷新后重试");
        }

        return ProductionOrderResult.Success(GetInternal(conn, request.OrderId)!);
    }

    public ProductionOrderResult Finish(ProductionOrderFinishRequest request)
    {
        if (request.FinishedQty <= 0)
        {
            return ProductionOrderResult.Fail(400, "完工数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.OrderId);
        if (current is null)
        {
            return ProductionOrderResult.Fail(404, "生产订单不存在");
        }

        if (current != ProductionStatusMap.Db.InProgress)
        {
            return ProductionOrderResult.Fail(409, "仅生产中订单可完工");
        }

        // 注：按当前分工，本阶段不联动库存（finish_inbound / stock_lock / material_stock 由 B 就绪后接入）。
        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PRODUCTION_ORDER
                                SET STATUS = :status, FINISHED_QTY = :finishedQty, ACTUAL_END = SYSDATE
                                WHERE ORDER_ID = :orderId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", ProductionStatusMap.Db.Completed));
            cmd.Parameters.Add(new OracleParameter("finishedQty", request.FinishedQty));
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("expected", ProductionStatusMap.Db.InProgress));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
        {
            return ProductionOrderResult.Fail(409, "订单状态已变更，请刷新后重试");
        }

        return ProductionOrderResult.Success(GetInternal(conn, request.OrderId)!);
    }

    public ProductionOrderResult Cancel(ProductionOrderActionRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.OrderId);
        if (current is null)
        {
            return ProductionOrderResult.Fail(404, "生产订单不存在");
        }

        if (current is ProductionStatusMap.Db.Completed or ProductionStatusMap.Db.Cancelled)
        {
            return ProductionOrderResult.Fail(409, "已完工或已取消订单不可取消");
        }

        int affected;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE PRODUCTION_ORDER SET STATUS = :status
                                WHERE ORDER_ID = :orderId
                                  AND STATUS NOT IN (:completed, :cancelled)";
            cmd.Parameters.Add(new OracleParameter("status", ProductionStatusMap.Db.Cancelled));
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("completed", ProductionStatusMap.Db.Completed));
            cmd.Parameters.Add(new OracleParameter("cancelled", ProductionStatusMap.Db.Cancelled));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
        {
            return ProductionOrderResult.Fail(409, "订单状态已变更，请刷新后重试");
        }

        return ProductionOrderResult.Success(GetInternal(conn, request.OrderId)!);
    }

    private static ProductionOrderDetail? GetInternal(OracleConnection conn, long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " WHERE po.ORDER_ID = :orderId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapDetail(reader) : null;
    }

    private static string? GetRawStatus(OracleConnection conn, long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM PRODUCTION_ORDER WHERE ORDER_ID = :orderId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static bool MaterialExists(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM MATERIAL WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool BomVersionExists(OracleConnection conn, long versionId, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM BOM_VERSION
                            WHERE VERSION_ID = :versionId AND MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static ProductionOrderDetail MapDetail(OracleDataReader reader) => new()
    {
        OrderId = Convert.ToInt64(reader.GetValue(0)),
        MaterialId = Convert.ToInt64(reader.GetValue(1)),
        MaterialName = reader.IsDBNull(2) ? null! : reader.GetString(2),
        PlanQty = reader.GetDecimal(3),
        FinishedQty = reader.GetDecimal(4),
        Status = ProductionStatusMap.FromDb(reader.GetString(5)),
        VersionId = Convert.ToInt64(reader.GetValue(6)),
        VersionNo = reader.IsDBNull(7) ? null! : reader.GetString(7),
        PlanStart = DateOnly.FromDateTime(reader.GetDateTime(8)),
        PlanEnd = DateOnly.FromDateTime(reader.GetDateTime(9)),
        ActualStart = reader.IsDBNull(10) ? null : DateOnly.FromDateTime(reader.GetDateTime(10)),
        ActualEnd = reader.IsDBNull(11) ? null : DateOnly.FromDateTime(reader.GetDateTime(11)),
        ReviewComment = null!,
    };
}
