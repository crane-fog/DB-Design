using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>外部订单单条操作结果。</summary>
public sealed record ExternalOrderResult(
    bool Ok,
    ExternalOrder? Order,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ExternalOrderResult Success(ExternalOrder order) => new(true, order, 200, null);

    public static ExternalOrderResult Fail(int code, string message) => new(false, null, code, message);
}

/// <summary>外部订单转生产订单结果。</summary>
public sealed record ExternalOrderConvertOutcome(
    bool Ok,
    ExternalOrderConvertResult? Result,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ExternalOrderConvertOutcome Success(ExternalOrderConvertResult result) =>
        new(true, result, 200, null);

    public static ExternalOrderConvertOutcome Fail(int code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// 外部订单主责 Service（C 模块）。维护 external_order 及 external_order_production 关联表。
/// 状态机：pending_review → accepted / rejected；仅 accepted 可转为正式生产订单。
/// 外部客户只能查询/提交自己的订单，customer_id 由登录态推导。
/// </summary>
public class ExternalOrderService(string connString, ILogger<ExternalOrderService> logger)
{
    private readonly ILogger<ExternalOrderService> _logger = logger;
    private const string SelectColumns = @"
        SELECT eo.EXT_ORDER_ID, eo.CUSTOMER_ID, u.USER_NAME, eo.MATERIAL_ID, m.MATERIAL_NAME,
               eo.QUANTITY, eo.EXPECTED_DATE, eo.CONTACT_PERSON, eo.CONTACT_PHONE,
               eo.STATUS, eo.SUBMIT_TIME, eo.REVIEW_COMMENT
        FROM EXTERNAL_ORDER eo
        LEFT JOIN SYS_USER u ON u.USER_ID = eo.CUSTOMER_ID
        LEFT JOIN MATERIAL m ON m.MATERIAL_ID = eo.MATERIAL_ID";

    public (List<ExternalOrder> Records, int Total) List(
        int page,
        int pageSize,
        long? customerId,
        string? dbStatus)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (customerId.HasValue)
        {
            where.Add("eo.CUSTOMER_ID = :customerId");
        }

        if (!string.IsNullOrEmpty(dbStatus))
        {
            where.Add("eo.STATUS = :status");
        }

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;

        void AddFilters(OracleCommand cmd)
        {
            if (customerId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("customerId", customerId.Value));
            }

            if (!string.IsNullOrEmpty(dbStatus))
            {
                cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            }
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM EXTERNAL_ORDER eo" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<ExternalOrder>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SelectColumns + whereClause +
                @" ORDER BY eo.EXT_ORDER_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapOrder(reader));
            }
        }

        return (records, total);
    }

    public ExternalOrderResult Create(ExternalOrderCreateRequest request, long customerId)
    {
        if (request.Quantity <= 0)
        {
            return ExternalOrderResult.Fail(400, "数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (!MaterialExists(conn, request.MaterialId))
        {
            return ExternalOrderResult.Fail(400, "产品不存在");
        }

        long newId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO EXTERNAL_ORDER
                                (CUSTOMER_ID, MATERIAL_ID, QUANTITY, EXPECTED_DATE,
                                 CONTACT_PERSON, CONTACT_PHONE, STATUS, SUBMIT_TIME)
                                VALUES (:customerId, :materialId, :quantity, :expectedDate,
                                        :contactPerson, :contactPhone, :status, SYSTIMESTAMP)
                                RETURNING EXT_ORDER_ID INTO :newId";
            cmd.Parameters.Add(new OracleParameter("customerId", customerId));
            cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
            cmd.Parameters.Add(new OracleParameter("quantity", request.Quantity));
            cmd.Parameters.Add(new OracleParameter("expectedDate", request.ExpectedDate.ToDateTime(TimeOnly.MinValue)));
            cmd.Parameters.Add(new OracleParameter("contactPerson", request.ContactPerson?.Trim()));
            cmd.Parameters.Add(new OracleParameter("contactPhone", request.ContactPhone?.Trim()));
            cmd.Parameters.Add(new OracleParameter("status", ExternalOrderStatusMap.Db.PendingReview));
            var idParam = new OracleParameter("newId", OracleDbType.Int64)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(idParam);
            cmd.ExecuteNonQuery();
            newId = Convert.ToInt64(idParam.Value.ToString());
        }

        return ExternalOrderResult.Success(GetInternal(conn, newId)!);
    }

    public ExternalOrderResult Review(ExternalOrderReviewRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.ExtOrderId);
        if (current is null)
        {
            return ExternalOrderResult.Fail(404, "外部订单不存在");
        }

        if (current != ExternalOrderStatusMap.Db.PendingReview)
        {
            return ExternalOrderResult.Fail(409, "仅待审核外部订单可审核");
        }

        var newStatus = request.Accepted ? ExternalOrderStatusMap.Db.Accepted : ExternalOrderStatusMap.Db.Rejected;
        int affected;
        using (var cmd = conn.CreateCommand())
        {
            // 状态作为 UPDATE 条件，确保并发下仅一次审核生效，避免绕过状态机重复审核。
            cmd.CommandText = @"UPDATE EXTERNAL_ORDER
                                SET STATUS = :status, REVIEW_COMMENT = :reviewComment
                                WHERE EXT_ORDER_ID = :extOrderId AND STATUS = :expected";
            cmd.Parameters.Add(new OracleParameter("status", newStatus));
            cmd.Parameters.Add(NullableString("reviewComment", request.ReviewComment));
            cmd.Parameters.Add(new OracleParameter("extOrderId", request.ExtOrderId));
            cmd.Parameters.Add(new OracleParameter("expected", ExternalOrderStatusMap.Db.PendingReview));
            affected = cmd.ExecuteNonQuery();
        }

        if (affected == 0)
        {
            return ExternalOrderResult.Fail(409, "外部订单状态已变更，请刷新后重试");
        }

        return ExternalOrderResult.Success(GetInternal(conn, request.ExtOrderId)!);
    }

    /// <summary>
    /// 将已接受的外部订单转换为一个或多个正式生产订单，并写入 external_order_production 关联表。
    /// 生产订单创建、状态校验和关联写入在同一事务内全部提交或全部回滚。
    /// </summary>
    public ExternalOrderConvertOutcome ConvertToProductionOrders(ExternalOrderConvertRequest request)
    {
        if (request.ProductionOrders is null || request.ProductionOrders.Count == 0)
        {
            return ExternalOrderConvertOutcome.Fail(400, "至少需要一个生产订单");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        var current = GetRawStatus(conn, request.ExtOrderId);
        if (current is null)
        {
            return ExternalOrderConvertOutcome.Fail(404, "外部订单不存在");
        }

        if (current != ExternalOrderStatusMap.Db.Accepted)
        {
            return ExternalOrderConvertOutcome.Fail(409, "仅已接受外部订单可转换");
        }

        // 预校验所有生产订单请求，避免部分提交后才发现非法输入。
        foreach (var po in request.ProductionOrders)
        {
            if (po.PlanQty <= 0)
            {
                return ExternalOrderConvertOutcome.Fail(400, "计划数量必须大于 0");
            }

            if (po.PlanEnd < po.PlanStart)
            {
                return ExternalOrderConvertOutcome.Fail(400, "计划完工日期不得早于计划开始日期");
            }

            if (!MaterialExists(conn, po.MaterialId))
            {
                return ExternalOrderConvertOutcome.Fail(400, "产品不存在");
            }

            if (!BomVersionExists(conn, po.VersionId, po.MaterialId))
            {
                return ExternalOrderConvertOutcome.Fail(400, "BOM 版本不存在或与产品不匹配");
            }
        }

        var briefs = new List<ProductionOrderBrief>();
        var associations = new List<ExternalOrderProductionAssociation>();

        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var po in request.ProductionOrders)
            {
                long orderId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO PRODUCTION_ORDER
                                        (MATERIAL_ID, VERSION_ID, PLAN_QTY, FINISHED_QTY, PLAN_START, PLAN_END, STATUS)
                                        VALUES (:materialId, :versionId, :planQty, 0, :planStart, :planEnd, :status)
                                        RETURNING ORDER_ID INTO :newId";
                    cmd.Parameters.Add(new OracleParameter("materialId", po.MaterialId));
                    cmd.Parameters.Add(new OracleParameter("versionId", po.VersionId));
                    cmd.Parameters.Add(new OracleParameter("planQty", po.PlanQty));
                    cmd.Parameters.Add(new OracleParameter("planStart", po.PlanStart.ToDateTime(TimeOnly.MinValue)));
                    cmd.Parameters.Add(new OracleParameter("planEnd", po.PlanEnd.ToDateTime(TimeOnly.MinValue)));
                    cmd.Parameters.Add(new OracleParameter("status", ProductionStatusMap.Db.PendingReview));
                    var idParam = new OracleParameter("newId", OracleDbType.Int64)
                    {
                        Direction = System.Data.ParameterDirection.Output,
                    };
                    cmd.Parameters.Add(idParam);
                    cmd.ExecuteNonQuery();
                    orderId = Convert.ToInt64(idParam.Value.ToString());
                }

                using (var assocCmd = conn.CreateCommand())
                {
                    assocCmd.Transaction = tx;
                    assocCmd.CommandText = @"INSERT INTO EXTERNAL_ORDER_PRODUCTION (EXT_ORDER_ID, ORDER_ID)
                                             VALUES (:extOrderId, :orderId)";
                    assocCmd.Parameters.Add(new OracleParameter("extOrderId", request.ExtOrderId));
                    assocCmd.Parameters.Add(new OracleParameter("orderId", orderId));
                    assocCmd.ExecuteNonQuery();
                }

                briefs.Add(new ProductionOrderBrief
                {
                    OrderId = orderId,
                    MaterialId = po.MaterialId,
                    MaterialName = GetMaterialName(conn, tx, po.MaterialId),
                    PlanQty = po.PlanQty,
                    FinishedQty = 0,
                    Status = ProductionOrderStatus.PendingReviewEnum,
                });
                associations.Add(new ExternalOrderProductionAssociation
                {
                    ExtOrderId = request.ExtOrderId,
                    OrderId = orderId,
                });
            }

            // 更新外部订单状态为"已转换"，使用 CAS 防止并发重复转换。
            using (var statusCmd = conn.CreateCommand())
            {
                statusCmd.Transaction = tx;
                statusCmd.CommandText = @"UPDATE EXTERNAL_ORDER
                                          SET STATUS = :newStatus
                                          WHERE EXT_ORDER_ID = :extOrderId AND STATUS = :expected";
                statusCmd.Parameters.Add(new OracleParameter("newStatus", ExternalOrderStatusMap.Db.Converted));
                statusCmd.Parameters.Add(new OracleParameter("extOrderId", request.ExtOrderId));
                statusCmd.Parameters.Add(new OracleParameter("expected", ExternalOrderStatusMap.Db.Accepted));
                var affected = statusCmd.ExecuteNonQuery();
                if (affected == 0)
                {
                    tx.Rollback();
                    return ExternalOrderConvertOutcome.Fail(409, "外部订单状态已变更，请刷新后重试");
                }
            }

            tx.Commit();
        }
        catch (OracleException ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "ConvertToProductionOrders 事务提交失败，extOrderId={ExtOrderId}", request.ExtOrderId);
            return ExternalOrderConvertOutcome.Fail(500, "外部订单转换失败，请稍后重试");
        }

        return ExternalOrderConvertOutcome.Success(new ExternalOrderConvertResult
        {
            ExtOrderId = request.ExtOrderId,
            ProductionOrders = briefs,
            Associations = associations,
        });
    }

    /// <summary>供 Controller 做外部客户数据自限校验：返回订单归属的 customer_id。</summary>
    public long? GetOwnerCustomerId(long extOrderId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CUSTOMER_ID FROM EXTERNAL_ORDER WHERE EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    private static ExternalOrder? GetInternal(OracleConnection conn, long extOrderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " WHERE eo.EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapOrder(reader) : null;
    }

    private static string? GetRawStatus(OracleConnection conn, long extOrderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM EXTERNAL_ORDER WHERE EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static string GetMaterialName(OracleConnection conn, OracleTransaction tx, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT MATERIAL_NAME FROM MATERIAL WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null! : value.ToString()!;
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

    private static OracleParameter NullableString(string name, string? value) =>
        new(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());

    private static ExternalOrder MapOrder(OracleDataReader reader) => new()
    {
        ExtOrderId = Convert.ToInt64(reader.GetValue(0)),
        CustomerId = Convert.ToInt64(reader.GetValue(1)),
        CustomerName = reader.IsDBNull(2) ? null! : reader.GetString(2),
        MaterialId = Convert.ToInt64(reader.GetValue(3)),
        MaterialName = reader.IsDBNull(4) ? null! : reader.GetString(4),
        Quantity = reader.GetDecimal(5),
        ExpectedDate = DateOnly.FromDateTime(reader.GetDateTime(6)),
        ContactPerson = reader.IsDBNull(7) ? null! : reader.GetString(7),
        ContactPhone = reader.IsDBNull(8) ? null! : reader.GetString(8),
        Status = ExternalOrderStatusMap.FromDb(reader.GetString(9)),
        SubmitTime = reader.GetDateTime(10),
        ReviewComment = reader.IsDBNull(11) ? null! : reader.GetString(11),
    };
}
