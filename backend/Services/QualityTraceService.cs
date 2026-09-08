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
            where.Add("trace.ORDER_ID = :orderId");
        }

        if (itemId.HasValue)
        {
            where.Add("trace.ITEM_ID = :itemId");
        }

        if (materialId.HasValue)
        {
            where.Add("trace.INPUT_MATERIAL_ID = :materialId");
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
            countCmd.CommandText = "SELECT COUNT(*) FROM V_BATCH_CONSUMPTION_DETAIL trace" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<BatchConsumption>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT trace.CONSUMPTION_ID, trace.ORDER_ID, trace.ITEM_ID, trace.CONSUME_QTY,
                       trace.PRODUCT_MATERIAL_ID, trace.PRODUCT_MATERIAL_NAME,
                       trace.PLAN_QTY, trace.FINISHED_QTY, trace.PRODUCTION_STATUS,
                       trace.PURCHASE_ORDER_ID, trace.INPUT_MATERIAL_ID,
                       trace.INPUT_MATERIAL_NAME, trace.PURCHASE_QUANTITY,
                       trace.RECEIVED_QTY, trace.UNIT_PRICE
                FROM V_BATCH_CONSUMPTION_DETAIL trace" + whereClause +
                @" ORDER BY trace.CONSUMPTION_ID DESC
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

    public BatchConsumption? GetConsumption(long consumptionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        return GetConsumption(conn, consumptionId);
    }

    public BatchConsumptionResult AddConsumption(BatchConsumptionCreateRequest request)
    {
        if (request.ConsumeQty <= 0)
        {
            return BatchConsumptionResult.Fail(400, "消耗数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            using var command = OracleCommandFactory.Create(
                conn,
                "BEGIN PKG_TRACE_DOMAIN.SAVE_CONSUMPTION(:consumptionId, :orderId, :itemId, :consumeQty); END;",
                transaction);
            var idParameter = new OracleParameter("consumptionId", OracleDbType.Int64)
            {
                Direction = System.Data.ParameterDirection.InputOutput,
                Value = DBNull.Value,
            };
            command.Parameters.Add(idParameter);
            command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
            command.Parameters.Add("itemId", OracleDbType.Int64).Value = request.ItemId;
            command.Parameters.Add("consumeQty", OracleDbType.Decimal).Value = request.ConsumeQty;
            command.ExecuteNonQuery();

            long newId = OracleCommandFactory.ReadIdentity(idParameter);
            BatchConsumption record = GetConsumption(conn, newId)
                ?? throw new InvalidOperationException("写入后无法读取消耗记录");
            transaction.Commit();
            return BatchConsumptionResult.Success(record);
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            transaction.Rollback();
            return BatchConsumptionResult.Fail(code, message);
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return BatchConsumptionResult.Fail(500, $"新增消耗记录失败: {exception.Message}");
        }
    }

    public BatchConsumptionResult UpdateConsumption(BatchConsumptionUpdateRequest request)
    {
        if (request.ConsumeQty <= 0)
        {
            return BatchConsumptionResult.Fail(400, "消耗数量必须大于 0");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            using var command = OracleCommandFactory.Create(
                conn,
                "BEGIN PKG_TRACE_DOMAIN.SAVE_CONSUMPTION(:consumptionId, :orderId, :itemId, :consumeQty); END;",
                transaction);
            var idParameter = new OracleParameter("consumptionId", OracleDbType.Int64)
            {
                Direction = System.Data.ParameterDirection.InputOutput,
                Value = request.ConsumptionId,
            };
            command.Parameters.Add(idParameter);
            command.Parameters.Add("orderId", OracleDbType.Int64).Value = request.OrderId;
            command.Parameters.Add("itemId", OracleDbType.Int64).Value = request.ItemId;
            command.Parameters.Add("consumeQty", OracleDbType.Decimal).Value = request.ConsumeQty;
            command.ExecuteNonQuery();

            BatchConsumption record = GetConsumption(conn, request.ConsumptionId)
                ?? throw new InvalidOperationException("更新后无法读取消耗记录");
            transaction.Commit();
            return BatchConsumptionResult.Success(record);
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            transaction.Rollback();
            return BatchConsumptionResult.Fail(code, message);
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return BatchConsumptionResult.Fail(500, $"更新消耗记录失败: {exception.Message}");
        }
    }

    public BatchConsumptionResult DeleteConsumption(long consumptionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            using var command = OracleCommandFactory.Create(
                conn,
                "BEGIN PKG_TRACE_DOMAIN.DELETE_CONSUMPTION(:consumptionId); END;",
                transaction);
            command.Parameters.Add("consumptionId", OracleDbType.Int64).Value = consumptionId;
            command.ExecuteNonQuery();
            transaction.Commit();
            return BatchConsumptionResult.Success(null!);
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            transaction.Rollback();
            return BatchConsumptionResult.Fail(code, message);
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return BatchConsumptionResult.Fail(500, $"删除消耗记录失败: {exception.Message}");
        }
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
                SELECT trace.ITEM_ID, trace.INPUT_MATERIAL_ID, trace.INPUT_MATERIAL_NAME,
                       trace.SUPPLIER_ID, trace.SUPPLIER_NAME, trace.PURCHASE_ORDER_ID,
                       trace.FIRST_RECEIVE_DATE, trace.CONSUME_QTY
                FROM V_PRODUCT_BATCH_TRACE trace
                WHERE trace.ORDER_ID = :orderId
                  AND (:batchNo IS NULL OR trace.BATCH_NO = :batchNo)
                ORDER BY trace.ITEM_ID";
            cmd.Parameters.Add(new OracleParameter("orderId", resolvedOrderId.Value));
            cmd.Parameters.Add(new OracleParameter(
                "batchNo",
                string.IsNullOrWhiteSpace(resolvedBatchNo) ? DBNull.Value : resolvedBatchNo));

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
        long? supplierId,
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

        if (supplierId.HasValue)
        {
            where.Add("pur.SUPPLIER_ID = :supplierId");
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

            if (supplierId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("supplierId", supplierId.Value));
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

        var affectedByItem = QueryAffectedProducts(conn, results.Select(result => result.ItemId));
        foreach (var result in results)
        {
            result.AffectedProducts = affectedByItem.GetValueOrDefault(result.ItemId) ?? [];
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
        var affected = QueryAffectedProducts(conn, itemIds)
            .Values
            .SelectMany(products => products)
            .ToList();
        var seenOrders = new HashSet<long>();
        var seenBatches = new HashSet<string>();

        foreach (var product in affected)
        {
            seenOrders.Add(product.OrderId);
            seenBatches.Add(!string.IsNullOrEmpty(product.BatchNo)
                ? product.BatchNo
                : $"order:{product.OrderId}");
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

    private static Dictionary<long, List<AffectedProductBatch>> QueryAffectedProducts(
        OracleConnection conn,
        IEnumerable<long> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        var productsByItem = ids.ToDictionary(id => id, _ => new List<AffectedProductBatch>());
        foreach (var chunk in ids.Chunk(900))
        {
            using var cmd = conn.CreateCommand();
            var placeholders = new List<string>();
            for (var index = 0; index < chunk.Length; index++)
            {
                var name = $"itemId{index}";
                placeholders.Add($":{name}");
                cmd.Parameters.Add(new OracleParameter(name, chunk[index]));
            }

            cmd.CommandText = $@"
                SELECT trace.ITEM_ID, trace.ORDER_ID, trace.BATCH_NO,
                       trace.PRODUCT_MATERIAL_ID, trace.PRODUCT_MATERIAL_NAME,
                       trace.PRODUCTION_STATUS, trace.CONSUME_QTY
                FROM V_MATERIAL_BATCH_TRACE trace
                WHERE trace.ITEM_ID IN ({string.Join(", ", placeholders)})
                ORDER BY trace.ITEM_ID, trace.ORDER_ID, trace.BATCH_NO";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var itemId = Convert.ToInt64(reader.GetValue(0));
                productsByItem[itemId].Add(new AffectedProductBatch
                {
                    OrderId = Convert.ToInt64(reader.GetValue(1)),
                    BatchNo = reader.IsDBNull(2) ? null! : reader.GetString(2),
                    ProductMaterialId = reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader.GetValue(3)),
                    ProductMaterialName = reader.IsDBNull(4) ? null! : reader.GetString(4),
                    ProductionStatus = reader.IsDBNull(5)
                        ? ProductionOrderStatus.PendingReviewEnum
                        : ProductionStatusMap.FromDb(reader.GetString(5)),
                    ConsumeQty = reader.GetDecimal(6),
                });
            }
        }

        return productsByItem;
    }

    private static BatchConsumption? GetConsumption(OracleConnection conn, long consumptionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT trace.CONSUMPTION_ID, trace.ORDER_ID, trace.ITEM_ID, trace.CONSUME_QTY,
                   trace.PRODUCT_MATERIAL_ID, trace.PRODUCT_MATERIAL_NAME,
                   trace.PLAN_QTY, trace.FINISHED_QTY, trace.PRODUCTION_STATUS,
                   trace.PURCHASE_ORDER_ID, trace.INPUT_MATERIAL_ID,
                   trace.INPUT_MATERIAL_NAME, trace.PURCHASE_QUANTITY,
                   trace.RECEIVED_QTY, trace.UNIT_PRICE
            FROM V_BATCH_CONSUMPTION_DETAIL trace
            WHERE trace.CONSUMPTION_ID = :consumptionId";
        cmd.Parameters.Add(new OracleParameter("consumptionId", consumptionId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapConsumption(reader) : null;
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
