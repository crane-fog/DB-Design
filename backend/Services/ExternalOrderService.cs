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

/// <summary>外部订单整单交货结果。</summary>
public sealed record ExternalOrderDeliveryOutcome(
    bool Ok,
    ExternalOrderDeliveryResult? Result,
    int ErrorCode,
    string? ErrorMessage)
{
    public static ExternalOrderDeliveryOutcome Success(ExternalOrderDeliveryResult result) =>
        new(true, result, 200, null);

    public static ExternalOrderDeliveryOutcome Fail(int code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// 外部订单主责 Service（C 模块）。维护 external_order、external_order_production
/// 及 external_order_delivery。
/// 状态机：pending_review → accepted / rejected；accepted → converted；converted → delivered。
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

    public (List<ExternalOrderListItem> Records, int Total) List(
        int page,
        int pageSize,
        long? customerId,
        string? customerName,
        string? dbStatus)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (customerId.HasValue)
        {
            where.Add("eo.CUSTOMER_ID = :customerId");
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            where.Add("INSTR(LOWER(u.USER_NAME), LOWER(:customerName)) > 0");
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

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                cmd.Parameters.Add(new OracleParameter("customerName", customerName.Trim()));
            }

            if (!string.IsNullOrEmpty(dbStatus))
            {
                cmd.Parameters.Add(new OracleParameter("status", dbStatus));
            }
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = @"SELECT COUNT(*)
                                     FROM EXTERNAL_ORDER eo
                                     LEFT JOIN SYS_USER u ON u.USER_ID = eo.CUSTOMER_ID" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var orders = new List<ExternalOrder>();
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
                orders.Add(MapOrder(reader));
            }
        }

        var records = orders.Select(order => BuildListItem(conn, order)).ToList();
        return (records, total);
    }

    public ExternalOrderFormOptions GetFormOptions(bool includeCustomers)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var customers = new List<ExternalOrderCustomerOption>();
        if (includeCustomers)
        {
            using var customerCmd = conn.CreateCommand();
            customerCmd.CommandText = @"SELECT DISTINCT u.USER_ID, u.EMPLOYEE_NO, u.USER_NAME
                                        FROM SYS_USER u
                                        JOIN SYS_USER_ROLE ur ON ur.USER_ID = u.USER_ID
                                        JOIN SYS_ROLE r ON r.ROLE_ID = ur.ROLE_ID
                                        WHERE u.STATUS = 'valid'
                                          AND r.STATUS = 'valid'
                                          AND r.ROLE_NAME = '外部客户'
                                        ORDER BY u.USER_NAME, u.USER_ID";
            using OracleDataReader customerReader = customerCmd.ExecuteReader();
            while (customerReader.Read())
            {
                customers.Add(new ExternalOrderCustomerOption
                {
                    UserId = Convert.ToInt64(customerReader.GetValue(0)),
                    EmployeeNo = customerReader.GetString(1),
                    UserName = customerReader.GetString(2),
                });
            }
        }

        var materials = new List<ExternalOrderMaterialOption>();
        using (var materialCmd = conn.CreateCommand())
        {
            materialCmd.CommandText = @"SELECT MATERIAL_ID, MATERIAL_NAME, MODEL, UNIT
                                        FROM MATERIAL
                                        WHERE MATERIAL_TYPE = '成品'
                                        ORDER BY MATERIAL_NAME, MATERIAL_ID";
            using OracleDataReader materialReader = materialCmd.ExecuteReader();
            while (materialReader.Read())
            {
                materials.Add(new ExternalOrderMaterialOption
                {
                    MaterialId = Convert.ToInt64(materialReader.GetValue(0)),
                    MaterialName = materialReader.GetString(1),
                    Model = materialReader.IsDBNull(2) ? string.Empty : materialReader.GetString(2),
                    Unit = materialReader.GetString(3),
                });
            }
        }

        return new ExternalOrderFormOptions
        {
            Customers = customers,
            Materials = materials,
        };
    }

    public ExternalOrderResult Create(
        ExternalOrderCreateRequest request,
        long customerId,
        bool requireExternalCustomer)
    {
        if (request.Quantity <= 0)
        {
            return ExternalOrderResult.Fail(400, "数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (requireExternalCustomer && !IsActiveExternalCustomer(conn, customerId))
        {
            return ExternalOrderResult.Fail(400, "所选客户不存在、已停用或不是外部客户");
        }

        if (!FinishedMaterialExists(conn, request.MaterialId))
        {
            return ExternalOrderResult.Fail(400, "所选产品不存在或不是成品物料");
        }

        long newId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO EXTERNAL_ORDER
                                (CUSTOMER_ID, MATERIAL_ID, QUANTITY, EXPECTED_DATE,
                                 CONTACT_PERSON, CONTACT_PHONE, STATUS, SUBMIT_TIME)
                                VALUES (:customerId, :materialId, :quantity, :expectedDate,
                                        :contactPerson, :contactPhone, :status, SYS_EXTRACT_UTC(SYSTIMESTAMP))
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

        var externalOrder = GetInternal(conn, request.ExtOrderId);
        if (externalOrder is null)
        {
            return ExternalOrderConvertOutcome.Fail(404, "外部订单不存在");
        }

        if (HasProductionAssociation(conn, request.ExtOrderId))
        {
            return ExternalOrderConvertOutcome.Fail(409, "外部订单已转换，请勿重复操作");
        }

        if (externalOrder.Status != ExternalOrderStatus.AcceptedEnum)
        {
            return ExternalOrderConvertOutcome.Fail(409, "仅已接受外部订单可转换");
        }

        foreach (ProductionOrderCreateRequest productionOrder in request.ProductionOrders)
        {
            if (productionOrder.PlanQty <= 0)
            {
                return ExternalOrderConvertOutcome.Fail(400, "计划数量必须大于 0");
            }

            if (productionOrder.PlanEnd < productionOrder.PlanStart)
            {
                return ExternalOrderConvertOutcome.Fail(400, "计划完工日期不得早于计划开始日期");
            }
        }

        if (request.ProductionOrders.Any(order => order.MaterialId != externalOrder.MaterialId))
        {
            return ExternalOrderConvertOutcome.Fail(409, "生产订单产品必须与外部订单产品一致");
        }

        var totalPlanQty = request.ProductionOrders.Sum(order => order.PlanQty);
        if (totalPlanQty != externalOrder.Quantity)
        {
            return ExternalOrderConvertOutcome.Fail(
                409,
                $"生产订单计划数量合计必须等于外部订单数量 {externalOrder.Quantity}");
        }

        var references = GetProductionOrderReferences(
            conn,
            request.ProductionOrders.Select(order => order.MaterialId).Distinct().ToList());

        // 预校验所有生产订单请求，避免部分提交后才发现非法输入。
        foreach (var po in request.ProductionOrders)
        {
            if (!references.MaterialNames.ContainsKey(po.MaterialId))
            {
                return ExternalOrderConvertOutcome.Fail(400, "产品不存在");
            }

            if (!references.BomVersions.Contains((po.VersionId, po.MaterialId)))
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
                    MaterialName = references.MaterialNames[po.MaterialId],
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

    /// <summary>
    /// 对已转换外部订单执行整单交货。关联订单校验、成品库存扣减、交货记录写入和
    /// 外部订单状态更新在同一事务内完成。
    /// </summary>
    public ExternalOrderDeliveryOutcome Deliver(long extOrderId, long operatorId)
    {
        if (extOrderId <= 0)
        {
            return ExternalOrderDeliveryOutcome.Fail(400, "外部订单编号必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            ExternalOrderDeliveryContext context = LockExternalOrder(conn, tx, extOrderId)
                ?? throw new ExternalOrderDeliveryException(404, "外部订单不存在");

            if (context.Status == ExternalOrderStatusMap.Db.Delivered)
            {
                throw new ExternalOrderDeliveryException(409, "外部订单已交货，请勿重复操作");
            }

            if (context.Status != ExternalOrderStatusMap.Db.Converted)
            {
                throw new ExternalOrderDeliveryException(409, "仅已转换外部订单可交货");
            }

            if (GetDelivery(conn, extOrderId, tx) is not null)
            {
                throw new ExternalOrderDeliveryException(409, "外部订单已存在交货记录");
            }

            List<ProductionOrderBrief> productionOrders = GetAssociatedProductionOrders(
                conn,
                extOrderId,
                tx,
                forUpdate: true);
            ValidateProductionOrdersForDelivery(context, productionOrders);

            decimal availableQty = GetAvailableStock(conn, context.MaterialId, tx, forUpdate: true)
                ?? throw new ExternalOrderDeliveryException(409, "成品库存不存在，无法交货");
            if (availableQty < context.Quantity)
            {
                throw new ExternalOrderDeliveryException(
                    409,
                    $"成品可用库存不足，需要 {context.Quantity}，当前可用 {availableQty}");
            }

            decimal remainingAvailableQty = availableQty - context.Quantity;
            UpdateFinishedStockForDelivery(
                conn,
                tx,
                context.MaterialId,
                context.Quantity);
            long deliveryId = InsertDelivery(
                conn,
                tx,
                extOrderId,
                context.MaterialId,
                context.Quantity,
                operatorId);
            UpdateExternalOrderDelivered(conn, tx, extOrderId);

            ExternalOrder updatedOrder = GetInternal(conn, extOrderId, tx)
                ?? throw new InvalidOperationException("交货后无法读取外部订单");
            ExternalOrderDelivery delivery = GetDelivery(conn, extOrderId, tx)
                ?? throw new InvalidOperationException($"交货后无法读取交货记录 {deliveryId}");
            ExternalOrderListItem listItem = BuildListItem(conn, updatedOrder, tx);

            tx.Commit();
            return ExternalOrderDeliveryOutcome.Success(new ExternalOrderDeliveryResult
            {
                ExternalOrder = listItem,
                Delivery = delivery,
                RemainingAvailableQty = remainingAvailableQty,
            });
        }
        catch (ExternalOrderDeliveryException exception)
        {
            tx.Rollback();
            return ExternalOrderDeliveryOutcome.Fail(exception.Code, exception.Message);
        }
        catch (OracleException exception) when (exception.Number == 1)
        {
            tx.Rollback();
            return ExternalOrderDeliveryOutcome.Fail(409, "外部订单已交货，请勿重复操作");
        }
        catch (Exception exception)
        {
            tx.Rollback();
            _logger.LogError(exception, "外部订单 {ExtOrderId} 整单交货失败", extOrderId);
            return ExternalOrderDeliveryOutcome.Fail(500, "外部订单交货失败，请稍后重试");
        }
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

    private static ExternalOrder? GetInternal(
        OracleConnection conn,
        long extOrderId,
        OracleTransaction? transaction = null)
    {
        using var cmd = conn.CreateCommand();
        if (transaction is not null)
        {
            cmd.Transaction = transaction;
        }
        cmd.CommandText = SelectColumns + " WHERE eo.EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapOrder(reader) : null;
    }

    private static ExternalOrderListItem BuildListItem(
        OracleConnection conn,
        ExternalOrder order,
        OracleTransaction? transaction = null)
    {
        List<ProductionOrderBrief> productionOrders = GetAssociatedProductionOrders(
            conn,
            order.ExtOrderId,
            transaction);
        ExternalOrderDelivery? delivery = GetDelivery(conn, order.ExtOrderId, transaction);
        decimal? availableQty = GetAvailableStock(conn, order.MaterialId, transaction);
        string? blockReason = GetDeliveryBlockReason(
            order,
            productionOrders,
            delivery,
            availableQty);

        return new ExternalOrderListItem
        {
            ExtOrderId = order.ExtOrderId,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            MaterialId = order.MaterialId,
            MaterialName = order.MaterialName,
            Quantity = order.Quantity,
            ExpectedDate = order.ExpectedDate,
            ContactPerson = order.ContactPerson,
            ContactPhone = order.ContactPhone,
            Status = order.Status,
            SubmitTime = order.SubmitTime,
            ReviewComment = order.ReviewComment,
            ProductionOrders = productionOrders,
            DeliveryReady = blockReason is null,
            DeliveryBlockReason = blockReason!,
            Delivery = delivery!,
        };
    }

    private static List<ProductionOrderBrief> GetAssociatedProductionOrders(
        OracleConnection conn,
        long extOrderId,
        OracleTransaction? transaction = null,
        bool forUpdate = false)
    {
        if (forUpdate)
        {
            using var lockCmd = conn.CreateCommand();
            if (transaction is not null)
            {
                lockCmd.Transaction = transaction;
            }
            lockCmd.CommandText = @"SELECT ORDER_ID
                                    FROM PRODUCTION_ORDER
                                    WHERE ORDER_ID IN (
                                        SELECT ORDER_ID
                                        FROM EXTERNAL_ORDER_PRODUCTION
                                        WHERE EXT_ORDER_ID = :extOrderId
                                    )
                                    FOR UPDATE";
            lockCmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
            using OracleDataReader lockReader = lockCmd.ExecuteReader();
            while (lockReader.Read())
            {
                // 读取全部行，确保所有关联生产订单均在当前事务中锁定。
            }
        }

        using var cmd = conn.CreateCommand();
        if (transaction is not null)
        {
            cmd.Transaction = transaction;
        }
        cmd.CommandText = @"SELECT po.ORDER_ID, po.MATERIAL_ID, m.MATERIAL_NAME,
                                   po.PLAN_QTY, po.FINISHED_QTY, po.STATUS
                            FROM EXTERNAL_ORDER_PRODUCTION eop
                            JOIN PRODUCTION_ORDER po ON po.ORDER_ID = eop.ORDER_ID
                            LEFT JOIN MATERIAL m ON m.MATERIAL_ID = po.MATERIAL_ID
                            WHERE eop.EXT_ORDER_ID = :extOrderId
                            ORDER BY po.ORDER_ID";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));

        var productionOrders = new List<ProductionOrderBrief>();
        using OracleDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            productionOrders.Add(new ProductionOrderBrief
            {
                OrderId = Convert.ToInt64(reader.GetValue(0)),
                MaterialId = Convert.ToInt64(reader.GetValue(1)),
                MaterialName = reader.IsDBNull(2) ? null! : reader.GetString(2),
                PlanQty = reader.GetDecimal(3),
                FinishedQty = reader.GetDecimal(4),
                Status = ProductionStatusMap.FromDb(reader.GetString(5)),
            });
        }

        return productionOrders;
    }

    private static ExternalOrderDelivery? GetDelivery(
        OracleConnection conn,
        long extOrderId,
        OracleTransaction? transaction = null)
    {
        using var cmd = conn.CreateCommand();
        if (transaction is not null)
        {
            cmd.Transaction = transaction;
        }
        cmd.CommandText = @"SELECT d.DELIVERY_ID, d.EXT_ORDER_ID, d.MATERIAL_ID, d.QUANTITY,
                                   d.DELIVERY_TIME, d.OPERATOR_ID, u.USER_NAME
                            FROM EXTERNAL_ORDER_DELIVERY d
                            LEFT JOIN SYS_USER u ON u.USER_ID = d.OPERATOR_ID
                            WHERE d.EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));

        using OracleDataReader reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ExternalOrderDelivery
        {
            DeliveryId = Convert.ToInt64(reader.GetValue(0)),
            ExtOrderId = Convert.ToInt64(reader.GetValue(1)),
            MaterialId = Convert.ToInt64(reader.GetValue(2)),
            Quantity = reader.GetDecimal(3),
            DeliveryTime = reader.GetUtcDateTime(4),
            OperatorId = Convert.ToInt64(reader.GetValue(5)),
            OperatorName = reader.IsDBNull(6) ? null! : reader.GetString(6),
        };
    }

    private static decimal? GetAvailableStock(
        OracleConnection conn,
        long materialId,
        OracleTransaction? transaction = null,
        bool forUpdate = false)
    {
        using var cmd = conn.CreateCommand();
        if (transaction is not null)
        {
            cmd.Transaction = transaction;
        }
        cmd.CommandText = "SELECT AVAILABLE_QTY FROM MATERIAL_STOCK WHERE MATERIAL_ID = :materialId";
        if (forUpdate)
        {
            cmd.CommandText += " FOR UPDATE";
        }

        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        object? value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToDecimal(value);
    }

    private static string? GetDeliveryBlockReason(
        ExternalOrder order,
        IReadOnlyList<ProductionOrderBrief> productionOrders,
        ExternalOrderDelivery? delivery,
        decimal? availableQty)
    {
        if (order.Status == ExternalOrderStatus.DeliveredEnum || delivery is not null)
        {
            return "外部订单已交货";
        }

        if (order.Status != ExternalOrderStatus.ConvertedEnum)
        {
            return order.Status switch
            {
                ExternalOrderStatus.PendingReviewEnum => "外部订单尚未审核",
                ExternalOrderStatus.AcceptedEnum => "外部订单尚未转换为生产订单",
                ExternalOrderStatus.RejectedEnum => "外部订单已拒绝",
                _ => "外部订单当前状态不可交货",
            };
        }

        if (productionOrders.Count == 0)
        {
            return "未关联生产订单";
        }

        if (productionOrders.Any(item => item.MaterialId != order.MaterialId))
        {
            return "关联生产订单产品与外部订单不一致";
        }

        if (productionOrders.Sum(item => item.PlanQty) != order.Quantity)
        {
            return "关联生产订单计划数量与外部订单数量不一致";
        }

        if (productionOrders.Any(item => item.Status != ProductionOrderStatus.CompletedEnum))
        {
            return "关联生产订单尚未全部完工";
        }

        if (!availableQty.HasValue)
        {
            return "成品库存不存在";
        }

        if (availableQty.Value < order.Quantity)
        {
            return $"成品可用库存不足，需要 {order.Quantity}，当前可用 {availableQty.Value}";
        }

        return null;
    }

    private static ExternalOrderDeliveryContext? LockExternalOrder(
        OracleConnection conn,
        OracleTransaction transaction,
        long extOrderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"SELECT MATERIAL_ID, QUANTITY, STATUS
                            FROM EXTERNAL_ORDER
                            WHERE EXT_ORDER_ID = :extOrderId
                            FOR UPDATE";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));

        using OracleDataReader reader = cmd.ExecuteReader();
        return reader.Read()
            ? new ExternalOrderDeliveryContext(
                Convert.ToInt64(reader.GetValue(0)),
                reader.GetDecimal(1),
                reader.GetString(2))
            : null;
    }

    private static void ValidateProductionOrdersForDelivery(
        ExternalOrderDeliveryContext externalOrder,
        IReadOnlyList<ProductionOrderBrief> productionOrders)
    {
        if (productionOrders.Count == 0)
        {
            throw new ExternalOrderDeliveryException(409, "外部订单未关联生产订单");
        }

        if (productionOrders.Any(order => order.MaterialId != externalOrder.MaterialId))
        {
            throw new ExternalOrderDeliveryException(409, "关联生产订单产品与外部订单不一致");
        }

        decimal totalPlanQty = productionOrders.Sum(order => order.PlanQty);
        if (totalPlanQty != externalOrder.Quantity)
        {
            throw new ExternalOrderDeliveryException(
                409,
                $"关联生产订单计划数量合计 {totalPlanQty} 与外部订单数量 {externalOrder.Quantity} 不一致");
        }

        long[] unfinishedOrderIds = productionOrders
            .Where(order => order.Status != ProductionOrderStatus.CompletedEnum)
            .Select(order => order.OrderId)
            .ToArray();
        if (unfinishedOrderIds.Length > 0)
        {
            throw new ExternalOrderDeliveryException(
                409,
                $"关联生产订单 {string.Join("、", unfinishedOrderIds.Select(id => $"#{id}"))} 尚未完工");
        }
    }

    private static void UpdateFinishedStockForDelivery(
        OracleConnection conn,
        OracleTransaction transaction,
        long materialId,
        decimal quantity)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"UPDATE MATERIAL_STOCK
                            SET AVAILABLE_QTY = AVAILABLE_QTY - :quantity,
                                LAST_OUT_DATE = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                            WHERE MATERIAL_ID = :materialId
                              AND AVAILABLE_QTY >= :requiredQuantity";
        cmd.Parameters.Add(new OracleParameter("quantity", quantity));
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("requiredQuantity", quantity));
        if (cmd.ExecuteNonQuery() != 1)
        {
            throw new ExternalOrderDeliveryException(409, "成品库存已变化，请刷新后重试");
        }
    }

    private static long InsertDelivery(
        OracleConnection conn,
        OracleTransaction transaction,
        long extOrderId,
        long materialId,
        decimal quantity,
        long operatorId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"INSERT INTO EXTERNAL_ORDER_DELIVERY
                                (EXT_ORDER_ID, MATERIAL_ID, QUANTITY, DELIVERY_TIME, OPERATOR_ID)
                            VALUES
                                (:extOrderId, :materialId, :quantity,
                                 SYS_EXTRACT_UTC(SYSTIMESTAMP), :operatorId)
                            RETURNING DELIVERY_ID INTO :newId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("quantity", quantity));
        cmd.Parameters.Add(new OracleParameter("operatorId", operatorId));
        var idParam = new OracleParameter("newId", OracleDbType.Int64)
        {
            Direction = System.Data.ParameterDirection.Output,
        };
        cmd.Parameters.Add(idParam);
        cmd.ExecuteNonQuery();
        return Convert.ToInt64(idParam.Value.ToString());
    }

    private static void UpdateExternalOrderDelivered(
        OracleConnection conn,
        OracleTransaction transaction,
        long extOrderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"UPDATE EXTERNAL_ORDER
                            SET STATUS = :newStatus
                            WHERE EXT_ORDER_ID = :extOrderId AND STATUS = :expectedStatus";
        cmd.Parameters.Add(new OracleParameter("newStatus", ExternalOrderStatusMap.Db.Delivered));
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
        cmd.Parameters.Add(new OracleParameter("expectedStatus", ExternalOrderStatusMap.Db.Converted));
        if (cmd.ExecuteNonQuery() != 1)
        {
            throw new ExternalOrderDeliveryException(409, "外部订单状态已变化，请刷新后重试");
        }
    }

    private static string? GetRawStatus(OracleConnection conn, long extOrderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT STATUS FROM EXTERNAL_ORDER WHERE EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : value.ToString();
    }

    private static bool HasProductionAssociation(OracleConnection conn, long extOrderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*)
                            FROM EXTERNAL_ORDER_PRODUCTION
                            WHERE EXT_ORDER_ID = :extOrderId";
        cmd.Parameters.Add(new OracleParameter("extOrderId", extOrderId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static ProductionOrderReferences GetProductionOrderReferences(
        OracleConnection conn,
        IReadOnlyList<long> materialIds)
    {
        var materialNames = new Dictionary<long, string>();
        var bomVersions = new HashSet<(long VersionId, long MaterialId)>();
        if (materialIds.Count == 0)
        {
            return new ProductionOrderReferences(materialNames, bomVersions);
        }

        using var cmd = conn.CreateCommand();
        var placeholders = new List<string>();
        for (var index = 0; index < materialIds.Count; index++)
        {
            var name = $"materialId{index}";
            placeholders.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, materialIds[index]));
        }

        cmd.CommandText = $@"SELECT m.MATERIAL_ID, m.MATERIAL_NAME, bv.VERSION_ID
                             FROM MATERIAL m
                             LEFT JOIN BOM_VERSION bv ON bv.MATERIAL_ID = m.MATERIAL_ID
                             WHERE m.MATERIAL_ID IN ({string.Join(", ", placeholders)})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var materialId = Convert.ToInt64(reader.GetValue(0));
            materialNames[materialId] = reader.GetString(1);
            if (!reader.IsDBNull(2))
            {
                bomVersions.Add((Convert.ToInt64(reader.GetValue(2)), materialId));
            }
        }

        return new ProductionOrderReferences(materialNames, bomVersions);
    }

    private static bool IsActiveExternalCustomer(OracleConnection conn, long customerId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*)
                            FROM SYS_USER u
                            JOIN SYS_USER_ROLE ur ON ur.USER_ID = u.USER_ID
                            JOIN SYS_ROLE r ON r.ROLE_ID = ur.ROLE_ID
                            WHERE u.USER_ID = :customerId
                              AND u.STATUS = 'valid'
                              AND r.STATUS = 'valid'
                              AND r.ROLE_NAME = '外部客户'";
        cmd.Parameters.Add(new OracleParameter("customerId", customerId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool FinishedMaterialExists(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*)
                            FROM MATERIAL
                            WHERE MATERIAL_ID = :materialId
                              AND MATERIAL_TYPE = '成品'";
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
        SubmitTime = reader.GetUtcDateTime(10),
        ReviewComment = reader.IsDBNull(11) ? null! : reader.GetString(11),
    };

    private sealed record ProductionOrderReferences(
        Dictionary<long, string> MaterialNames,
        HashSet<(long VersionId, long MaterialId)> BomVersions);

    private sealed record ExternalOrderDeliveryContext(
        long MaterialId,
        decimal Quantity,
        string Status);

    private sealed class ExternalOrderDeliveryException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }
}
