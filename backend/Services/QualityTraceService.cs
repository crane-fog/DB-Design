using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>批次消耗单条操作结果。</summary>
public sealed record BatchConsumptionResult(
    bool Ok,
    BatchConsumption? Record,
    int ErrorCode,
    string? ErrorMessage)
{
    public static BatchConsumptionResult Success(BatchConsumption record) => new(true, record, 200, null);

    public static BatchConsumptionResult Fail(int code, string message) => new(false, null, code, message);
}

/// <summary>
/// 质量追溯主责 Service（C 模块）。维护 batch_consumption（生产订单↔采购明细的消耗关系），
/// 并通过只读 JOIN 组装正向追溯（成品→原材料）、反向追溯（原材料→成品）和质量影响分析。
/// 采购明细 / 供应商 / 收货 / 物料等权威实体由 A、B 模块维护，本 Service 只查询不写入。
/// </summary>
public class QualityTraceService(string connString)
{
    public (List<BatchConsumption> Records, int Total) ListConsumption(
        int page,
        int pageSize,
        long? orderId,
        long? itemId,
        long? materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (orderId.HasValue)
        {
            where.Add("bc.ORDER_ID = :orderId");
        }

        if (itemId.HasValue)
        {
            where.Add("bc.ITEM_ID = :itemId");
        }

        if (materialId.HasValue)
        {
            where.Add("poi.MATERIAL_ID = :materialId");
        }

        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;

        void AddFilters(OracleCommand cmd)
        {
            if (orderId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("orderId", orderId.Value));
            }

            if (itemId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("itemId", itemId.Value));
            }

            if (materialId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            }
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = @"SELECT COUNT(*) FROM BATCH_CONSUMPTION bc
                                     JOIN PURCHASE_ORDER_ITEM poi ON poi.ITEM_ID = bc.ITEM_ID" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<BatchConsumption>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT bc.CONSUMPTION_ID, bc.ORDER_ID, bc.ITEM_ID, bc.CONSUME_QTY,
                       po.MATERIAL_ID, pm.MATERIAL_NAME, po.PLAN_QTY, po.FINISHED_QTY, po.STATUS,
                       poi.ORDER_ID, poi.MATERIAL_ID, im.MATERIAL_NAME, poi.QUANTITY, poi.RECEIVED_QTY, poi.UNIT_PRICE
                FROM BATCH_CONSUMPTION bc
                JOIN PURCHASE_ORDER_ITEM poi ON poi.ITEM_ID = bc.ITEM_ID
                LEFT JOIN PRODUCTION_ORDER po ON po.ORDER_ID = bc.ORDER_ID
                LEFT JOIN MATERIAL pm ON pm.MATERIAL_ID = po.MATERIAL_ID
                LEFT JOIN MATERIAL im ON im.MATERIAL_ID = poi.MATERIAL_ID" + whereClause +
                @" ORDER BY bc.CONSUMPTION_ID DESC
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapConsumption(reader));
            }
        }

        return (records, total);
    }

    public BatchConsumptionResult AddConsumption(BatchConsumptionCreateRequest request)
    {
        if (request.ConsumeQty <= 0)
        {
            return BatchConsumptionResult.Fail(400, "消耗数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (!ProductionOrderExists(conn, request.OrderId))
        {
            return BatchConsumptionResult.Fail(400, "生产订单不存在");
        }

        if (!OrderHasActualStart(conn, request.OrderId))
        {
            return BatchConsumptionResult.Fail(409, "仅已开工生产订单可录入消耗");
        }

        if (!PurchaseItemExists(conn, request.ItemId))
        {
            return BatchConsumptionResult.Fail(400, "采购订单明细不存在");
        }

        if (ConsumptionExists(conn, request.OrderId, request.ItemId))
        {
            return BatchConsumptionResult.Fail(409, "该原材料已存在消耗记录，请使用更新接口");
        }

        long newId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO BATCH_CONSUMPTION (ORDER_ID, ITEM_ID, CONSUME_QTY)
                                VALUES (:orderId, :itemId, :consumeQty)
                                RETURNING CONSUMPTION_ID INTO :newId";
            cmd.Parameters.Add(new OracleParameter("orderId", request.OrderId));
            cmd.Parameters.Add(new OracleParameter("itemId", request.ItemId));
            cmd.Parameters.Add(new OracleParameter("consumeQty", request.ConsumeQty));
            var idParam = new OracleParameter("newId", OracleDbType.Int64)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(idParam);
            cmd.ExecuteNonQuery();
            newId = Convert.ToInt64(idParam.Value.ToString());
        }

        return BatchConsumptionResult.Success(GetConsumption(conn, newId)!);
    }

    public BatchConsumptionResult UpdateConsumption(BatchConsumptionUpdateRequest request)
    {
        if (request.ConsumeQty <= 0)
        {
            return BatchConsumptionResult.Fail(400, "消耗数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (!ConsumptionExists(conn, request.ConsumptionId))
        {
            return BatchConsumptionResult.Fail(404, "批次消耗关系不存在");
        }

        if (!ProductionOrderExists(conn, request.OrderId))
        {
            return BatchConsumptionResult.Fail(400, "生产订单不存在");
        }

        if (!OrderHasActualStart(conn, request.OrderId))
        {
            return BatchConsumptionResult.Fail(409, "仅已开工生产订单可录入消耗");
        }

        if (!PurchaseItemExists(conn, request.ItemId))
        {
            return BatchConsumptionResult.Fail(400, "采购订单明细不存在");
        }

        using (var cmd = conn.CreateCommand())
        {
            // 消耗记录归属特定生产订单不可变更（仅允许修正 item_id 和 consume_qty），
            // 如需跨订单调整应删除后重新录入（终态订单不可删除以保证追溯链完整）。
            cmd.CommandText = @"UPDATE BATCH_CONSUMPTION
                                SET ITEM_ID = :itemId, CONSUME_QTY = :consumeQty
                                WHERE CONSUMPTION_ID = :consumptionId";
            cmd.Parameters.Add(new OracleParameter("itemId", request.ItemId));
            cmd.Parameters.Add(new OracleParameter("consumeQty", request.ConsumeQty));
            cmd.Parameters.Add(new OracleParameter("consumptionId", request.ConsumptionId));
            cmd.ExecuteNonQuery();
        }

        return BatchConsumptionResult.Success(GetConsumption(conn, request.ConsumptionId)!);
    }

    public BatchConsumptionResult DeleteConsumption(long consumptionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        if (!ConsumptionExists(conn, consumptionId))
        {
            return BatchConsumptionResult.Fail(404, "批次消耗关系不存在");
        }

        // 仅 in_progress 状态允许删除，防止追溯链断裂。
        if (!IsConsumptionOrderInProgress(conn, consumptionId))
        {
            return BatchConsumptionResult.Fail(409, "仅生产中订单可删除消耗记录");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM BATCH_CONSUMPTION WHERE CONSUMPTION_ID = :consumptionId";
        cmd.Parameters.Add(new OracleParameter("consumptionId", consumptionId));
        cmd.ExecuteNonQuery();

        return BatchConsumptionResult.Success(null!);
    }

    /// <summary>
    /// 正向追溯：从生产订单或成品批次号出发，查出该成品消耗的原材料采购批次、供应商和到货信息。
    /// order_id 与 batch_no 至少提供一个。
    /// </summary>
    public ProductBatchTraceResult? TraceProductBatch(long? orderId, string? batchNo, bool includeSupplier)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var resolvedOrderId = orderId;
        string? resolvedBatchNo = batchNo;

        // 若仅提供批次号，则通过完工入库单反查生产订单。
        if (resolvedOrderId is null && !string.IsNullOrWhiteSpace(batchNo))
        {
            using var lookup = conn.CreateCommand();
            lookup.CommandText = "SELECT ORDER_ID FROM FINISH_INBOUND WHERE BATCH_NO = :batchNo FETCH FIRST 1 ROWS ONLY";
            lookup.Parameters.Add(new OracleParameter("batchNo", batchNo.Trim()));
            var value = lookup.ExecuteScalar();
            if (value is null or DBNull)
            {
                return null;
            }

            resolvedOrderId = Convert.ToInt64(value);
        }

        if (resolvedOrderId is null)
        {
            return null;
        }

        long productMaterialId;
        string? productMaterialName;
        using (var head = conn.CreateCommand())
        {
            head.CommandText = @"SELECT po.MATERIAL_ID, m.MATERIAL_NAME
                                 FROM PRODUCTION_ORDER po
                                 LEFT JOIN MATERIAL m ON m.MATERIAL_ID = po.MATERIAL_ID
                                 WHERE po.ORDER_ID = :orderId";
            head.Parameters.Add(new OracleParameter("orderId", resolvedOrderId.Value));
            using var headReader = head.ExecuteReader();
            if (!headReader.Read())
            {
                return null;
            }

            productMaterialId = Convert.ToInt64(headReader.GetValue(0));
            productMaterialName = headReader.IsDBNull(1) ? null : headReader.GetString(1);
        }

        // 若未显式给出批次号，取该订单最近一条完工入库批次用于展示。
        if (string.IsNullOrWhiteSpace(resolvedBatchNo))
        {
            using var batchCmd = conn.CreateCommand();
            batchCmd.CommandText = @"SELECT BATCH_NO FROM FINISH_INBOUND
                                     WHERE ORDER_ID = :orderId
                                     ORDER BY INBOUND_TIME DESC FETCH FIRST 1 ROWS ONLY";
            batchCmd.Parameters.Add(new OracleParameter("orderId", resolvedOrderId.Value));
            var batchValue = batchCmd.ExecuteScalar();
            resolvedBatchNo = batchValue is null or DBNull ? null : batchValue.ToString();
        }

        var consumed = new List<ConsumedMaterialBatch>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT bc.ITEM_ID, poi.MATERIAL_ID, im.MATERIAL_NAME,
                       pur.SUPPLIER_ID, s.SUPPLIER_NAME, poi.ORDER_ID,
                       (SELECT MIN(rr.RECEIVE_DATE) FROM RECEIVE_RECORD rr
                        WHERE rr.ORDER_ID = poi.ORDER_ID AND rr.MATERIAL_ID = poi.MATERIAL_ID) AS RECEIVE_DATE,
                       bc.CONSUME_QTY
                FROM BATCH_CONSUMPTION bc
                JOIN PURCHASE_ORDER_ITEM poi ON poi.ITEM_ID = bc.ITEM_ID
                LEFT JOIN PURCHASE_ORDER pur ON pur.ORDER_ID = poi.ORDER_ID
                LEFT JOIN SUPPLIER s ON s.SUPPLIER_ID = pur.SUPPLIER_ID
                LEFT JOIN MATERIAL im ON im.MATERIAL_ID = poi.MATERIAL_ID
                WHERE bc.ORDER_ID = :orderId
                ORDER BY bc.ITEM_ID";
            cmd.Parameters.Add(new OracleParameter("orderId", resolvedOrderId.Value));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                consumed.Add(new ConsumedMaterialBatch
                {
                    ItemId = Convert.ToInt64(reader.GetValue(0)),
                    MaterialId = Convert.ToInt64(reader.GetValue(1)),
                    MaterialName = reader.IsDBNull(2) ? null! : reader.GetString(2),
                    SupplierId = includeSupplier && !reader.IsDBNull(3) ? Convert.ToInt64(reader.GetValue(3)) : null,
                    SupplierName = includeSupplier && !reader.IsDBNull(4) ? reader.GetString(4) : null!,
                    OrderId = reader.IsDBNull(5) ? null : Convert.ToInt64(reader.GetValue(5)),
                    ReceiveDate = reader.IsDBNull(6) ? null : DateOnly.FromDateTime(reader.GetDateTime(6)),
                    ConsumeQty = reader.GetDecimal(7),
                });
            }
        }

        return new ProductBatchTraceResult
        {
            OrderId = resolvedOrderId.Value,
            BatchNo = resolvedBatchNo!,
            MaterialId = productMaterialId,
            MaterialName = productMaterialName!,
            ConsumedBatches = consumed,
        };
    }

    /// <summary>
    /// 反向追溯：从采购明细 / 原材料 / 到货日期范围出发，查出问题批次流入的所有生产订单和成品批次。
    /// item_id、material_id 或完整到货日期范围至少提供一种。
    /// </summary>
    public List<MaterialBatchTraceResult> TraceMaterialBatch(
        long? itemId,
        long? materialId,
        DateOnly? receiveDateStart,
        DateOnly? receiveDateEnd)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = new List<string>();
        if (itemId.HasValue)
        {
            where.Add("poi.ITEM_ID = :itemId");
        }

        if (materialId.HasValue)
        {
            where.Add("poi.MATERIAL_ID = :materialId");
        }

        if (receiveDateStart.HasValue && receiveDateEnd.HasValue)
        {
            where.Add(@"EXISTS (SELECT 1 FROM RECEIVE_RECORD rr
                                WHERE rr.ORDER_ID = poi.ORDER_ID AND rr.MATERIAL_ID = poi.MATERIAL_ID
                                  AND rr.RECEIVE_DATE BETWEEN :receiveStart AND :receiveEnd)");
        }

        if (where.Count == 0)
        {
            return [];
        }

        var results = new List<MaterialBatchTraceResult>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT poi.ITEM_ID, poi.MATERIAL_ID, im.MATERIAL_NAME, pur.SUPPLIER_ID, s.SUPPLIER_NAME
                FROM PURCHASE_ORDER_ITEM poi
                LEFT JOIN PURCHASE_ORDER pur ON pur.ORDER_ID = poi.ORDER_ID
                LEFT JOIN SUPPLIER s ON s.SUPPLIER_ID = pur.SUPPLIER_ID
                LEFT JOIN MATERIAL im ON im.MATERIAL_ID = poi.MATERIAL_ID
                WHERE " + string.Join(" AND ", where) + " ORDER BY poi.ITEM_ID";
            if (itemId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("itemId", itemId.Value));
            }

            if (materialId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            }

            if (receiveDateStart.HasValue && receiveDateEnd.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("receiveStart", receiveDateStart.Value.ToDateTime(TimeOnly.MinValue)));
                cmd.Parameters.Add(new OracleParameter("receiveEnd", receiveDateEnd.Value.ToDateTime(TimeOnly.MaxValue)));
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new MaterialBatchTraceResult
                {
                    ItemId = Convert.ToInt64(reader.GetValue(0)),
                    MaterialId = Convert.ToInt64(reader.GetValue(1)),
                    MaterialName = reader.IsDBNull(2) ? null! : reader.GetString(2),
                    SupplierId = reader.IsDBNull(3) ? null : Convert.ToInt64(reader.GetValue(3)),
                    SupplierName = reader.IsDBNull(4) ? null! : reader.GetString(4),
                    AffectedProducts = [],
                });
            }
        }

        foreach (var result in results)
        {
            result.AffectedProducts = QueryAffectedProducts(conn, result.ItemId);
        }

        return results;
    }

    /// <summary>
    /// 质量影响分析：按问题采购明细 / 原材料 / 到货日期范围，汇总受影响生产订单、成品批次和建议动作。
    /// </summary>
    public QualityImpactAnalyzeResult AnalyzeImpact(QualityImpactAnalyzeRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var itemIds = ResolveImpactItemIds(conn, request);
        var affected = new List<AffectedProductBatch>();
        var seenOrders = new HashSet<long>();
        var seenBatches = new HashSet<string>();

        foreach (var itemId in itemIds)
        {
            foreach (var product in QueryAffectedProducts(conn, itemId))
            {
                affected.Add(product);
                seenOrders.Add(product.OrderId);
                seenBatches.Add(!string.IsNullOrEmpty(product.BatchNo)
                    ? product.BatchNo
                    : $"order:{product.OrderId}");
            }
        }

        var orderCount = seenOrders.Count;
        var batchCount = seenBatches.Count;

        // 建议动作：影响面越大越倾向冻结/召回。
        QualityImpactAnalyzeResult.SuggestedActionEnum action;
        if (orderCount == 0)
        {
            action = QualityImpactAnalyzeResult.SuggestedActionEnum.ObserveEnum;
        }
        else if (orderCount >= 3 || batchCount >= 5)
        {
            action = QualityImpactAnalyzeResult.SuggestedActionEnum.RecallEnum;
        }
        else
        {
            action = QualityImpactAnalyzeResult.SuggestedActionEnum.FreezeEnum;
        }

        return new QualityImpactAnalyzeResult
        {
            AffectedOrderCount = orderCount,
            AffectedBatchCount = batchCount,
            AffectedProducts = affected,
            SuggestedAction = action,
        };
    }

    private List<long> ResolveImpactItemIds(OracleConnection conn, QualityImpactAnalyzeRequest request)
    {
        if (request.ItemIds is { Count: > 0 })
        {
            return request.ItemIds.Distinct().ToList();
        }

        var where = new List<string>();
        if (request.MaterialId != 0)
        {
            where.Add("poi.MATERIAL_ID = :materialId");
        }

        if (request.ReceiveDateStart != default && request.ReceiveDateEnd != default)
        {
            where.Add(@"EXISTS (SELECT 1 FROM RECEIVE_RECORD rr
                                WHERE rr.ORDER_ID = poi.ORDER_ID AND rr.MATERIAL_ID = poi.MATERIAL_ID
                                  AND rr.RECEIVE_DATE BETWEEN :receiveStart AND :receiveEnd)");
        }

        if (where.Count == 0)
        {
            return [];
        }

        var ids = new List<long>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT poi.ITEM_ID FROM PURCHASE_ORDER_ITEM poi WHERE " + string.Join(" AND ", where);
        if (request.MaterialId != 0)
        {
            cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
        }

        if (request.ReceiveDateStart != default && request.ReceiveDateEnd != default)
        {
            cmd.Parameters.Add(new OracleParameter("receiveStart", request.ReceiveDateStart.ToDateTime(TimeOnly.MinValue)));
            cmd.Parameters.Add(new OracleParameter("receiveEnd", request.ReceiveDateEnd.ToDateTime(TimeOnly.MaxValue)));
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(Convert.ToInt64(reader.GetValue(0)));
        }

        return ids;
    }

    private static List<AffectedProductBatch> QueryAffectedProducts(OracleConnection conn, long itemId)
    {
        var products = new List<AffectedProductBatch>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT bc.ORDER_ID,
                   (SELECT MAX(fi.BATCH_NO) FROM FINISH_INBOUND fi WHERE fi.ORDER_ID = bc.ORDER_ID) AS BATCH_NO,
                   po.MATERIAL_ID, m.MATERIAL_NAME, po.STATUS, bc.CONSUME_QTY
            FROM BATCH_CONSUMPTION bc
            LEFT JOIN PRODUCTION_ORDER po ON po.ORDER_ID = bc.ORDER_ID
            LEFT JOIN MATERIAL m ON m.MATERIAL_ID = po.MATERIAL_ID
            WHERE bc.ITEM_ID = :itemId
            ORDER BY bc.ORDER_ID";
        cmd.Parameters.Add(new OracleParameter("itemId", itemId));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new AffectedProductBatch
            {
                OrderId = Convert.ToInt64(reader.GetValue(0)),
                BatchNo = reader.IsDBNull(1) ? null! : reader.GetString(1),
                ProductMaterialId = reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2)),
                ProductMaterialName = reader.IsDBNull(3) ? null! : reader.GetString(3),
                ProductionStatus = reader.IsDBNull(4)
                    ? ProductionOrderStatus.PendingReviewEnum
                    : ProductionStatusMap.FromDb(reader.GetString(4)),
                ConsumeQty = reader.GetDecimal(5),
            });
        }

        return products;
    }

    private static BatchConsumption? GetConsumption(OracleConnection conn, long consumptionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT bc.CONSUMPTION_ID, bc.ORDER_ID, bc.ITEM_ID, bc.CONSUME_QTY,
                   po.MATERIAL_ID, pm.MATERIAL_NAME, po.PLAN_QTY, po.FINISHED_QTY, po.STATUS,
                   poi.ORDER_ID, poi.MATERIAL_ID, im.MATERIAL_NAME, poi.QUANTITY, poi.RECEIVED_QTY, poi.UNIT_PRICE
            FROM BATCH_CONSUMPTION bc
            JOIN PURCHASE_ORDER_ITEM poi ON poi.ITEM_ID = bc.ITEM_ID
            LEFT JOIN PRODUCTION_ORDER po ON po.ORDER_ID = bc.ORDER_ID
            LEFT JOIN MATERIAL pm ON pm.MATERIAL_ID = po.MATERIAL_ID
            LEFT JOIN MATERIAL im ON im.MATERIAL_ID = poi.MATERIAL_ID
            WHERE bc.CONSUMPTION_ID = :consumptionId";
        cmd.Parameters.Add(new OracleParameter("consumptionId", consumptionId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapConsumption(reader) : null;
    }

    private static bool ProductionOrderExists(OracleConnection conn, long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PRODUCTION_ORDER WHERE ORDER_ID = :orderId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool OrderHasActualStart(OracleConnection conn, long orderId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PRODUCTION_ORDER WHERE ORDER_ID = :orderId AND ACTUAL_START IS NOT NULL";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool PurchaseItemExists(OracleConnection conn, long itemId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PURCHASE_ORDER_ITEM WHERE ITEM_ID = :itemId";
        cmd.Parameters.Add(new OracleParameter("itemId", itemId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool ConsumptionExists(OracleConnection conn, long consumptionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM BATCH_CONSUMPTION WHERE CONSUMPTION_ID = :consumptionId";
        cmd.Parameters.Add(new OracleParameter("consumptionId", consumptionId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool ConsumptionExists(OracleConnection conn, long orderId, long itemId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM BATCH_CONSUMPTION WHERE ORDER_ID = :orderId AND ITEM_ID = :itemId";
        cmd.Parameters.Add(new OracleParameter("orderId", orderId));
        cmd.Parameters.Add(new OracleParameter("itemId", itemId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool IsConsumptionOrderInProgress(OracleConnection conn, long consumptionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM BATCH_CONSUMPTION bc
            JOIN PRODUCTION_ORDER po ON po.ORDER_ID = bc.ORDER_ID
            WHERE bc.CONSUMPTION_ID = :consumptionId AND TRIM(po.STATUS) = :inProgress";
        cmd.Parameters.Add(new OracleParameter("consumptionId", consumptionId));
        cmd.Parameters.Add(new OracleParameter("inProgress", ProductionStatusMap.Db.InProgress));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static BatchConsumption MapConsumption(OracleDataReader reader)
    {
        var record = new BatchConsumption
        {
            ConsumptionId = Convert.ToInt64(reader.GetValue(0)),
            OrderId = Convert.ToInt64(reader.GetValue(1)),
            ItemId = Convert.ToInt64(reader.GetValue(2)),
            ConsumeQty = reader.GetDecimal(3),
        };

        if (!reader.IsDBNull(4))
        {
            record.ProductionOrder = new ProductionOrderBrief
            {
                OrderId = record.OrderId,
                MaterialId = Convert.ToInt64(reader.GetValue(4)),
                MaterialName = reader.IsDBNull(5) ? null! : reader.GetString(5),
                PlanQty = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                FinishedQty = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                Status = reader.IsDBNull(8)
                    ? ProductionOrderStatus.PendingReviewEnum
                    : ProductionStatusMap.FromDb(reader.GetString(8)),
            };
        }

        record.PurchaseItem = new PurchaseOrderDetailLine
        {
            ItemId = record.ItemId,
            OrderId = Convert.ToInt64(reader.GetValue(9)),
            MaterialId = Convert.ToInt64(reader.GetValue(10)),
            MaterialName = reader.IsDBNull(11) ? null! : reader.GetString(11),
            Quantity = reader.GetDecimal(12),
            ReceivedQty = reader.GetDecimal(13),
            UnitPrice = reader.GetDecimal(14),
        };

        return record;
    }
}
