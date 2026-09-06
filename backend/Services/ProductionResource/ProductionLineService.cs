using System.Data;

using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed class ProductionLineService(string connString) : IProductionLineService
{
    private const int DuplicateKeyError = 1;

    public ProductionResourceResult<ProductionResourcePage<LineType>> ListLineTypes(
        int page,
        int pageSize,
        string? typeName)
    {
        try
        {
            using OracleConnection connection = OpenConnection();
            string? normalizedName = string.IsNullOrWhiteSpace(typeName) ? null : typeName.Trim();
            string whereClause = normalizedName is null
                ? string.Empty
                : " WHERE UPPER(TYPE_NAME) LIKE UPPER(:typeName)";

            int total;
            using (OracleCommand countCommand = OracleCommandFactory.Create(
                       connection,
                       "SELECT COUNT(*) FROM LINE_TYPE" + whereClause))
            {
                AddTypeNameFilter(countCommand, normalizedName);
                total = Convert.ToInt32(countCommand.ExecuteScalar());
            }

            List<LineType> records = [];
            using (OracleCommand command = OracleCommandFactory.Create(
                       connection,
                       @"SELECT TYPE_ID, TYPE_NAME
                         FROM LINE_TYPE" + whereClause +
                       @" ORDER BY TYPE_ID
                          OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY"))
            {
                AddTypeNameFilter(command, normalizedName);
                command.Parameters.Add("skip", OracleDbType.Int32).Value = (page - 1) * pageSize;
                command.Parameters.Add("take", OracleDbType.Int32).Value = pageSize;

                using OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(MapLineType(reader));
                }
            }

            return ProductionResourceResult<ProductionResourcePage<LineType>>.Success(
                new ProductionResourcePage<LineType>(records, total, page, pageSize),
                "查询成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionResourcePage<LineType>>.Fail(
                500,
                "查询生产线类型失败");
        }
    }

    public ProductionResourceResult<LineType> SaveLineType(LineTypeSaveRequest request)
    {
        string typeName = request.TypeName?.Trim() ?? string.Empty;
        if (typeName.Length == 0 || typeName.Length > 50)
        {
            return ProductionResourceResult<LineType>.Fail(400, "生产线类型名称长度必须为 1 到 50");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            long typeId;

            if (request.TypeId is > 0)
            {
                using OracleCommand command = OracleCommandFactory.Create(
                    connection,
                    @"UPDATE LINE_TYPE
                      SET TYPE_NAME = :typeName
                      WHERE TYPE_ID = :typeId");
                command.Parameters.Add("typeName", OracleDbType.Varchar2).Value = typeName;
                command.Parameters.Add("typeId", OracleDbType.Int64).Value = request.TypeId.Value;

                if (command.ExecuteNonQuery() == 0)
                {
                    return ProductionResourceResult<LineType>.Fail(404, "生产线类型不存在");
                }

                typeId = request.TypeId.Value;
            }
            else
            {
                using OracleCommand command = OracleCommandFactory.Create(
                    connection,
                    @"INSERT INTO LINE_TYPE (TYPE_NAME)
                      VALUES (:typeName)
                      RETURNING TYPE_ID INTO :newId");
                command.Parameters.Add("typeName", OracleDbType.Varchar2).Value = typeName;
                OracleParameter idParameter = new("newId", OracleDbType.Int64)
                {
                    Direction = ParameterDirection.Output,
                };
                command.Parameters.Add(idParameter);
                command.ExecuteNonQuery();
                typeId = OracleCommandFactory.ReadIdentity(idParameter);
            }

            LineType? result = GetLineType(connection, typeId);
            return result is null
                ? ProductionResourceResult<LineType>.Fail(500, "保存后无法读取生产线类型")
                : ProductionResourceResult<LineType>.Success(result, "保存成功");
        }
        catch (OracleException exception) when (exception.Number == DuplicateKeyError)
        {
            return ProductionResourceResult<LineType>.Fail(409, "生产线类型名称已存在");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<LineType>.Fail(500, "保存生产线类型失败");
        }
    }

    public ProductionResourceResult<ProductionResourcePage<ProductionLine>> ListLines(
        int page,
        int pageSize,
        long? typeId,
        ProductionLineRunStatus? status)
    {
        string? databaseStatus = status.HasValue
            ? ProductionLineRunStatusMap.ToDbOrNull(status.Value)
            : null;
        if (status.HasValue && databaseStatus is null)
        {
            return ProductionResourceResult<ProductionResourcePage<ProductionLine>>.Fail(
                400,
                "生产线状态无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            List<string> filters = [];
            if (typeId.HasValue)
            {
                filters.Add("pl.TYPE_ID = :typeId");
            }

            if (databaseStatus is not null)
            {
                filters.Add("NVL(ls.STATUS, '空闲') = :status");
            }

            string whereClause = filters.Count == 0
                ? string.Empty
                : " WHERE " + string.Join(" AND ", filters);

            int total;
            using (OracleCommand countCommand = OracleCommandFactory.Create(
                       connection,
                       @"SELECT COUNT(*)
                         FROM PRODUCTION_LINE pl
                         LEFT JOIN LINE_STATUS ls ON ls.LINE_ID = pl.LINE_ID" + whereClause))
            {
                AddLineFilters(countCommand, typeId, databaseStatus);
                total = Convert.ToInt32(countCommand.ExecuteScalar());
            }

            List<ProductionLine> records = [];
            using (OracleCommand command = OracleCommandFactory.Create(
                       connection,
                       LineSelect + whereClause +
                       @" ORDER BY pl.LINE_ID
                          OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY"))
            {
                AddLineFilters(command, typeId, databaseStatus);
                command.Parameters.Add("skip", OracleDbType.Int32).Value = (page - 1) * pageSize;
                command.Parameters.Add("take", OracleDbType.Int32).Value = pageSize;

                using OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(MapLine(reader));
                }
            }

            return ProductionResourceResult<ProductionResourcePage<ProductionLine>>.Success(
                new ProductionResourcePage<ProductionLine>(records, total, page, pageSize),
                "查询成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionResourcePage<ProductionLine>>.Fail(
                500,
                "查询生产线失败");
        }
    }

    public ProductionResourceResult<ProductionLine> AddLine(ProductionLineCreateRequest request)
    {
        if (request.TypeId <= 0 || request.ManagerId <= 0 || request.StartDate == default)
        {
            return ProductionResourceResult<ProductionLine>.Fail(400, "生产线参数不完整");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            if (!LineTypeExists(connection, request.TypeId))
            {
                return ProductionResourceResult<ProductionLine>.Fail(404, "生产线类型不存在");
            }

            if (!ActiveUserExists(connection, request.ManagerId))
            {
                return ProductionResourceResult<ProductionLine>.Fail(404, "生产线负责人不存在或已停用");
            }

            using OracleTransaction transaction = connection.BeginTransaction();
            try
            {
                long lineId;
                using (OracleCommand command = OracleCommandFactory.Create(
                           connection,
                           @"INSERT INTO PRODUCTION_LINE (TYPE_ID, START_DATE, MANAGER_ID)
                             VALUES (:typeId, :startDate, :managerId)
                             RETURNING LINE_ID INTO :newId",
                           transaction))
                {
                    command.Parameters.Add("typeId", OracleDbType.Int64).Value = request.TypeId;
                    command.Parameters.Add("startDate", OracleDbType.Date).Value =
                        request.StartDate.ToDateTime(TimeOnly.MinValue);
                    command.Parameters.Add("managerId", OracleDbType.Int64).Value = request.ManagerId;
                    OracleParameter idParameter = new("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    command.Parameters.Add(idParameter);
                    command.ExecuteNonQuery();
                    lineId = OracleCommandFactory.ReadIdentity(idParameter);
                }

                using (OracleCommand statusCommand = OracleCommandFactory.Create(
                           connection,
                           @"INSERT INTO LINE_STATUS
                             (LINE_ID, STATUS, CURRENT_ORDER_ID, CURRENT_MATERIAL_ID,
                              FINISHED_QTY, EFFICIENCY, UPDATED_TIME)
                             VALUES (:lineId, :status, NULL, NULL, 0, 0, SYS_EXTRACT_UTC(SYSTIMESTAMP))",
                           transaction))
                {
                    statusCommand.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
                    statusCommand.Parameters.Add("status", OracleDbType.Varchar2).Value =
                        ProductionLineRunStatusMap.Db.Idle;
                    statusCommand.ExecuteNonQuery();
                }

                ProductionLine? result = GetLine(connection, lineId, transaction);
                if (result is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(
                        500,
                        "新增后无法读取生产线");
                }

                transaction.Commit();
                return ProductionResourceResult<ProductionLine>.Success(result, "新增成功");
            }
            catch (OracleException)
            {
                transaction.Rollback();
                return ProductionResourceResult<ProductionLine>.Fail(500, "新增生产线失败");
            }
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionLine>.Fail(500, "新增生产线失败");
        }
    }

    public ProductionResourceResult<ProductionLine> UpdateLine(ProductionLineUpdateRequest request)
    {
        if (request.LineId <= 0
            || request.TypeId <= 0
            || request.ManagerId <= 0
            || request.StartDate == default)
        {
            return ProductionResourceResult<ProductionLine>.Fail(400, "生产线参数不完整");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            using OracleTransaction transaction = connection.BeginTransaction();
            try
            {
                if (!LineTypeExists(connection, request.TypeId, transaction))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(404, "生产线类型不存在");
                }

                if (!ActiveUserExists(connection, request.ManagerId, transaction))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(404, "生产线负责人不存在或已停用");
                }

                ProductionLineUpdateContext? currentLine = GetLineUpdateContext(
                    connection,
                    transaction,
                    request.LineId);
                if (currentLine is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(404, "生产线不存在");
                }

                if (currentLine.TypeId != request.TypeId
                    && HasCalendarTypeConflict(
                        connection,
                        transaction,
                        request.LineId,
                        request.TypeId))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(
                        409,
                        "生产线类型与已有生产日历的产能配置不匹配");
                }

                if (currentLine.StartDate != request.StartDate
                    && HasCalendarBeforeStartDate(
                        connection,
                        transaction,
                        request.LineId,
                        request.StartDate))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(
                        409,
                        "生产线启用日期不得晚于已有排产日期");
                }

                using OracleCommand command = OracleCommandFactory.Create(
                    connection,
                    @"UPDATE PRODUCTION_LINE
                      SET TYPE_ID = :typeId,
                          START_DATE = :startDate,
                          MANAGER_ID = :managerId
                      WHERE LINE_ID = :lineId",
                    transaction);
                command.Parameters.Add("typeId", OracleDbType.Int64).Value = request.TypeId;
                command.Parameters.Add("startDate", OracleDbType.Date).Value =
                    request.StartDate.ToDateTime(TimeOnly.MinValue);
                command.Parameters.Add("managerId", OracleDbType.Int64).Value = request.ManagerId;
                command.Parameters.Add("lineId", OracleDbType.Int64).Value = request.LineId;
                command.ExecuteNonQuery();

                ProductionLine? result = GetLine(connection, request.LineId, transaction);
                if (result is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLine>.Fail(
                        500,
                        "修改后无法读取生产线");
                }

                transaction.Commit();
                return ProductionResourceResult<ProductionLine>.Success(result, "修改成功");
            }
            catch (OracleException)
            {
                transaction.Rollback();
                return ProductionResourceResult<ProductionLine>.Fail(500, "修改生产线失败");
            }
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionLine>.Fail(500, "修改生产线失败");
        }
    }

    public ProductionResourceResult<FaultRecord> ReportFault(
        FaultRecordCreateRequest request,
        CurrentUser currentUser)
    {
        string faultType = request.FaultType?.Trim() ?? string.Empty;
        string description = request.Description?.Trim() ?? string.Empty;
        if (request.LineId <= 0
            || faultType.Length is 0 or > 50
            || description.Length is 0 or > 500)
        {
            return ProductionResourceResult<FaultRecord>.Fail(400, "故障上报参数无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            if (!LineExists(connection, request.LineId))
            {
                return ProductionResourceResult<FaultRecord>.Fail(404, "生产线不存在");
            }

            using OracleTransaction transaction = connection.BeginTransaction();
            try
            {
                long faultId;
                using (OracleCommand command = OracleCommandFactory.Create(
                           connection,
                           @"INSERT INTO FAULT_RECORD
                             (LINE_ID, FAULT_TYPE, DESCRIPTION, OCCUR_TIME, RECOVER_TIME,
                              STATUS, REPORTER_ID, REPAIRER_ID)
                             VALUES (:lineId, :faultType, :description, SYS_EXTRACT_UTC(SYSTIMESTAMP), NULL,
                                     :status, :reporterId, NULL)
                             RETURNING FAULT_ID INTO :newId",
                           transaction))
                {
                    command.Parameters.Add("lineId", OracleDbType.Int64).Value = request.LineId;
                    command.Parameters.Add("faultType", OracleDbType.Varchar2).Value = faultType;
                    command.Parameters.Add("description", OracleDbType.Varchar2).Value = description;
                    command.Parameters.Add("status", OracleDbType.Varchar2).Value =
                        FaultStatusMap.Db.PendingRepair;
                    command.Parameters.Add("reporterId", OracleDbType.Int64).Value = currentUser.UserId;
                    OracleParameter idParameter = new("newId", OracleDbType.Int64)
                    {
                        Direction = ParameterDirection.Output,
                    };
                    command.Parameters.Add(idParameter);
                    command.ExecuteNonQuery();
                    faultId = OracleCommandFactory.ReadIdentity(idParameter);
                }

                UpsertFaultLineStatus(connection, transaction, request.LineId);
                FaultRecord? result = GetFault(connection, faultId, transaction);
                if (result is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<FaultRecord>.Fail(
                        500,
                        "上报后无法读取故障");
                }

                transaction.Commit();
                return ProductionResourceResult<FaultRecord>.Success(result, "故障已上报");
            }
            catch (OracleException)
            {
                transaction.Rollback();
                return ProductionResourceResult<FaultRecord>.Fail(500, "故障上报失败");
            }
        }
        catch (OracleException)
        {
            return ProductionResourceResult<FaultRecord>.Fail(500, "故障上报失败");
        }
    }

    public ProductionResourceResult<FaultRecord> UpdateFault(
        FaultRecordUpdateRequest request,
        CurrentUser currentUser)
    {
        string? requestedStatus = FaultStatusMap.ToDbOrNull(request.Status);
        if (request.FaultId <= 0 || requestedStatus is null)
        {
            return ProductionResourceResult<FaultRecord>.Fail(400, "故障更新参数无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            using OracleTransaction transaction = connection.BeginTransaction();
            try
            {
                FaultRecord? current = GetFault(connection, request.FaultId, transaction, true);
                if (current is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<FaultRecord>.Fail(404, "故障记录不存在");
                }

                string currentStatus = FaultStatusMap.ToDbOrNull(current.Status)
                    ?? FaultStatusMap.Db.PendingRepair;
                long? effectiveRepairerId = request.RepairerId ?? current.RepairerId;
                bool isCurrentRepairer = current.RepairerId == currentUser.UserId;
                bool currentRepairerRemainsAssigned = isCurrentRepairer
                    && (!request.RepairerId.HasValue
                        || request.RepairerId == currentUser.UserId);
                bool isClaimingSelf = currentUser.HasPermission(PermissionCode.ProductionFaultClaimEnum)
                    && currentStatus == FaultStatusMap.Db.PendingRepair
                    && !current.RepairerId.HasValue
                    && request.RepairerId == currentUser.UserId;
                bool mayUpdateAssigned = currentUser.HasPermission(PermissionCode.ProductionFaultUpdateAssignedEnum)
                    && currentRepairerRemainsAssigned;
                if (!currentUser.HasPermission(PermissionCode.ProductionFaultUpdateAnyEnum)
                    && !mayUpdateAssigned
                    && !isClaimingSelf)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<FaultRecord>.Fail(403, "无权更新该故障");
                }

                if (!FaultStatusMap.CanTransition(currentStatus, requestedStatus))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<FaultRecord>.Fail(409, "故障状态流转非法");
                }

                if (requestedStatus == FaultStatusMap.Db.Recovered
                    && request.RecoverTime.HasValue
                    && request.RecoverTime.Value < current.OccurTime)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<FaultRecord>.Fail(
                        400,
                        "恢复时间不得早于故障发生时间");
                }

                if (requestedStatus is FaultStatusMap.Db.Repairing or FaultStatusMap.Db.Recovered)
                {
                    if (!effectiveRepairerId.HasValue
                        || !ActiveUserExists(connection, effectiveRepairerId.Value, transaction))
                    {
                        transaction.Rollback();
                        return ProductionResourceResult<FaultRecord>.Fail(
                            404,
                            "维修负责人不存在或已停用");
                    }
                }

                using (OracleCommand updateCommand = OracleCommandFactory.Create(
                           connection,
                           @"UPDATE FAULT_RECORD
                             SET STATUS = :newStatus,
                                 REPAIRER_ID = :repairerId,
                                 RECOVER_TIME =
                                     CASE WHEN :newStatus = '已恢复'
                                          THEN CASE
                                              WHEN :currentStatus = '已恢复'
                                              THEN NVL(:recoverTime, RECOVER_TIME)
                                              ELSE NVL(:recoverTime, SYS_EXTRACT_UTC(SYSTIMESTAMP))
                                          END
                                          ELSE NULL
                                     END
                             WHERE FAULT_ID = :faultId
                               AND STATUS = :currentStatus",
                           transaction))
                {
                    updateCommand.Parameters.Add("newStatus", OracleDbType.Varchar2).Value =
                        requestedStatus;
                    updateCommand.Parameters.Add("repairerId", OracleDbType.Int64).Value =
                        OracleCommandFactory.DbValue(effectiveRepairerId);
                    updateCommand.Parameters.Add("recoverTime", OracleDbType.TimeStamp).Value =
                        OracleCommandFactory.DbValue(request.RecoverTime);
                    updateCommand.Parameters.Add("faultId", OracleDbType.Int64).Value = request.FaultId;
                    updateCommand.Parameters.Add("currentStatus", OracleDbType.Varchar2).Value =
                        currentStatus;

                    if (updateCommand.ExecuteNonQuery() == 0)
                    {
                        transaction.Rollback();
                        return ProductionResourceResult<FaultRecord>.Fail(
                            409,
                            "故障状态已被其他操作更新");
                    }
                }

                if (requestedStatus == FaultStatusMap.Db.Recovered
                    && CountActiveFaults(connection, transaction, current.LineId, request.FaultId) == 0)
                {
                    RestoreIdleLineStatus(connection, transaction, current.LineId);
                }

                FaultRecord? result = GetFault(connection, request.FaultId, transaction);
                if (result is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<FaultRecord>.Fail(
                        500,
                        "更新后无法读取故障");
                }

                transaction.Commit();
                return ProductionResourceResult<FaultRecord>.Success(result, "故障状态已更新");
            }
            catch (OracleException)
            {
                transaction.Rollback();
                return ProductionResourceResult<FaultRecord>.Fail(500, "更新故障失败");
            }
        }
        catch (OracleException)
        {
            return ProductionResourceResult<FaultRecord>.Fail(500, "更新故障失败");
        }
    }

    public ProductionResourceResult<ProductionResourcePage<FaultRecord>> ListFaults(
        int page,
        int pageSize,
        long? lineId,
        FaultStatus? status)
    {
        try
        {
            using OracleConnection connection = OpenConnection();
            string whereClause = "1 = 1";
            string? dbStatus = status.HasValue ? FaultStatusMap.ToDbOrNull(status.Value) : null;
            if (lineId.HasValue)
            {
                whereClause += " AND LINE_ID = :lineId";
            }

            if (dbStatus is not null)
            {
                whereClause += " AND STATUS = :status";
            }

            using OracleCommand countCommand = OracleCommandFactory.Create(
                connection,
                $"SELECT COUNT(*) FROM FAULT_RECORD WHERE {whereClause}");
            if (lineId.HasValue)
            {
                countCommand.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId.Value;
            }

            if (dbStatus is not null)
            {
                countCommand.Parameters.Add("status", OracleDbType.Varchar2).Value = dbStatus;
            }

            long total = Convert.ToInt64(countCommand.ExecuteScalar());
            if (total == 0)
            {
                return ProductionResourceResult<ProductionResourcePage<FaultRecord>>.Success(
                    new ProductionResourcePage<FaultRecord>([], 0, page, pageSize),
                    "查询成功");
            }

            long offset = (page - 1) * pageSize;
            using OracleCommand selectCommand = OracleCommandFactory.Create(
                connection,
                $@"SELECT FAULT_ID, LINE_ID, FAULT_TYPE, DESCRIPTION, OCCUR_TIME,
                          RECOVER_TIME, STATUS, REPORTER_ID, REPAIRER_ID
                   FROM FAULT_RECORD
                   WHERE {whereClause}
                   ORDER BY OCCUR_TIME DESC
                   OFFSET :offset ROWS FETCH NEXT :pageSize ROWS ONLY");
            selectCommand.Parameters.Add("offset", OracleDbType.Int64).Value = offset;
            selectCommand.Parameters.Add("pageSize", OracleDbType.Int32).Value = pageSize;
            if (lineId.HasValue)
            {
                selectCommand.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId.Value;
            }

            if (dbStatus is not null)
            {
                selectCommand.Parameters.Add("status", OracleDbType.Varchar2).Value = dbStatus;
            }

            List<FaultRecord> records = [];
            using (OracleDataReader reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    records.Add(MapFault(reader));
                }
            }

            return ProductionResourceResult<ProductionResourcePage<FaultRecord>>.Success(
                new ProductionResourcePage<FaultRecord>(
                    records,
                    (int)total,
                    page,
                    pageSize),
                "查询成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionResourcePage<FaultRecord>>.Fail(
                500,
                "查询故障列表失败");
        }
    }

    public ProductionResourceResult<ProductionLineStatus> UpdateLineStatus(
        ProductionLineStatusUpdateRequest request,
        CurrentUser currentUser)
    {
        string? databaseStatus = ProductionLineRunStatusMap.ToDbOrNull(request.Status);
        if (request.LineId <= 0
            || databaseStatus is null
            || request.FinishedQty < 0
            || request.Efficiency is < 0 or > 1)
        {
            return ProductionResourceResult<ProductionLineStatus>.Fail(400, "生产线状态参数无效");
        }

        if (databaseStatus == ProductionLineRunStatusMap.Db.Idle
            && (request.CurrentOrderId.HasValue || request.CurrentMaterialId.HasValue))
        {
            return ProductionResourceResult<ProductionLineStatus>.Fail(
                400,
                "空闲状态不得关联生产订单或产品");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            if (!LineExists(connection, request.LineId))
            {
                return ProductionResourceResult<ProductionLineStatus>.Fail(404, "生产线不存在");
            }

            using OracleTransaction transaction = connection.BeginTransaction();
            try
            {
                ProductionLineStatus? currentStatus = GetLineStatus(
                    connection,
                    request.LineId,
                    transaction,
                    forUpdate: true);

                if (databaseStatus == ProductionLineRunStatusMap.Db.Running
                    && HasActiveFaults(connection, transaction, request.LineId))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLineStatus>.Fail(
                        409,
                        "生产线存在未恢复故障，无法切换为运行状态");
                }

                long? effectiveMaterialId = request.CurrentMaterialId;
                bool sameProductionContext = currentStatus is not null
                    && (request.CurrentOrderId.HasValue
                        ? currentStatus.CurrentOrderId == request.CurrentOrderId
                        : currentStatus.CurrentOrderId is null
                            && currentStatus.CurrentMaterialId == request.CurrentMaterialId);
                decimal effectiveFinishedQty = request.FinishedQty
                    ?? (sameProductionContext ? currentStatus!.FinishedQty : 0m);

                if (request.CurrentOrderId.HasValue)
                {
                    ProductionOrderLineContext? order = GetProductionOrderLineContext(
                        connection,
                        transaction,
                        request.CurrentOrderId.Value);
                    if (order is null)
                    {
                        transaction.Rollback();
                        return ProductionResourceResult<ProductionLineStatus>.Fail(
                            404,
                            "生产订单不存在");
                    }

                    if (databaseStatus == ProductionLineRunStatusMap.Db.Running
                        && order.Status != ProductionStatusMap.Db.InProgress)
                    {
                        transaction.Rollback();
                        return ProductionResourceResult<ProductionLineStatus>.Fail(
                            409,
                            "运行状态只能关联生产中的订单");
                    }

                    if (effectiveMaterialId.HasValue && effectiveMaterialId != order.MaterialId)
                    {
                        transaction.Rollback();
                        return ProductionResourceResult<ProductionLineStatus>.Fail(
                            400,
                            "当前产品与生产订单不匹配");
                    }

                    effectiveMaterialId = order.MaterialId;
                    if (effectiveFinishedQty > order.PlanQty)
                    {
                        transaction.Rollback();
                        return ProductionResourceResult<ProductionLineStatus>.Fail(
                            400,
                            "已完成数量不得超过生产订单计划数量");
                    }
                }
                else if (effectiveMaterialId.HasValue
                    && !MaterialExists(connection, transaction, effectiveMaterialId.Value))
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLineStatus>.Fail(
                        404,
                        "当前产品不存在");
                }

                decimal previousFinishedQty = sameProductionContext
                    ? currentStatus!.FinishedQty
                    : 0m;
                if (effectiveFinishedQty < previousFinishedQty)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLineStatus>.Fail(
                        409,
                        "同一生产上下文的已完成数量不得回退");
                }

                decimal outputIncrement = effectiveFinishedQty - previousFinishedQty;

                using (OracleCommand command = OracleCommandFactory.Create(
                           connection,
                           @"MERGE INTO LINE_STATUS target
                             USING (SELECT :lineId AS LINE_ID FROM DUAL) source
                             ON (target.LINE_ID = source.LINE_ID)
                             WHEN MATCHED THEN UPDATE SET
                                 target.STATUS = :status,
                                 target.CURRENT_ORDER_ID = :currentOrderId,
                                 target.CURRENT_MATERIAL_ID = :currentMaterialId,
                                 target.FINISHED_QTY = :finishedQty,
                                 target.EFFICIENCY = NVL(:efficiency, target.EFFICIENCY),
                                 target.UPDATED_TIME = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                             WHEN NOT MATCHED THEN INSERT
                                 (LINE_ID, STATUS, CURRENT_ORDER_ID, CURRENT_MATERIAL_ID,
                                  FINISHED_QTY, EFFICIENCY, UPDATED_TIME)
                             VALUES
                                 (:lineId, :status, :currentOrderId, :currentMaterialId,
                                  :finishedQty, NVL(:efficiency, 0), SYS_EXTRACT_UTC(SYSTIMESTAMP))",
                           transaction))
                {
                    command.Parameters.Add("lineId", OracleDbType.Int64).Value = request.LineId;
                    command.Parameters.Add("status", OracleDbType.Varchar2).Value = databaseStatus;
                    command.Parameters.Add("currentOrderId", OracleDbType.Int64).Value =
                        OracleCommandFactory.DbValue(request.CurrentOrderId);
                    command.Parameters.Add("currentMaterialId", OracleDbType.Int64).Value =
                        OracleCommandFactory.DbValue(effectiveMaterialId);
                    command.Parameters.Add("finishedQty", OracleDbType.Decimal).Value =
                        effectiveFinishedQty;
                    command.Parameters.Add("efficiency", OracleDbType.Decimal).Value =
                        OracleCommandFactory.DbValue(request.Efficiency);
                    command.ExecuteNonQuery();
                }

                if (outputIncrement > 0)
                {
                    InsertOutputRecord(
                        connection,
                        transaction,
                        request.LineId,
                        request.CurrentOrderId,
                        outputIncrement,
                        currentUser.UserId);
                }

                ProductionLineStatus? result = GetLineStatus(
                    connection,
                    request.LineId,
                    transaction);
                if (result is null)
                {
                    transaction.Rollback();
                    return ProductionResourceResult<ProductionLineStatus>.Fail(
                        500,
                        "更新后无法读取产线状态");
                }

                transaction.Commit();
                return ProductionResourceResult<ProductionLineStatus>.Success(
                    result,
                    "产线状态已更新");
            }
            catch (OracleException)
            {
                transaction.Rollback();
                return ProductionResourceResult<ProductionLineStatus>.Fail(500, "更新产线状态失败");
            }
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionLineStatus>.Fail(500, "更新产线状态失败");
        }
    }

    private const string LineSelect = @"
        SELECT pl.LINE_ID,
               pl.TYPE_ID,
               lt.TYPE_NAME,
               pl.START_DATE,
               pl.MANAGER_ID,
               u.USER_NAME,
               NVL(ls.STATUS, '空闲')
        FROM PRODUCTION_LINE pl
        JOIN LINE_TYPE lt ON lt.TYPE_ID = pl.TYPE_ID
        JOIN SYS_USER u ON u.USER_ID = pl.MANAGER_ID
        LEFT JOIN LINE_STATUS ls ON ls.LINE_ID = pl.LINE_ID";

    private OracleConnection OpenConnection()
    {
        OracleConnection connection = new(connString);
        connection.Open();
        return connection;
    }

    private static void AddTypeNameFilter(OracleCommand command, string? typeName)
    {
        if (typeName is not null)
        {
            command.Parameters.Add("typeName", OracleDbType.Varchar2).Value = $"%{typeName}%";
        }
    }

    private static void AddLineFilters(
        OracleCommand command,
        long? typeId,
        string? status)
    {
        if (typeId.HasValue)
        {
            command.Parameters.Add("typeId", OracleDbType.Int64).Value = typeId.Value;
        }

        if (status is not null)
        {
            command.Parameters.Add("status", OracleDbType.Varchar2).Value = status;
        }
    }

    private static LineType MapLineType(OracleDataReader reader) => new()
    {
        TypeId = Convert.ToInt64(reader.GetValue(0)),
        TypeName = reader.GetString(1),
    };

    private static ProductionLine MapLine(OracleDataReader reader) => new()
    {
        LineId = Convert.ToInt64(reader.GetValue(0)),
        TypeId = Convert.ToInt64(reader.GetValue(1)),
        TypeName = reader.GetString(2),
        StartDate = DateOnly.FromDateTime(reader.GetDateTime(3)),
        ManagerId = Convert.ToInt64(reader.GetValue(4)),
        ManagerName = reader.GetString(5),
        Status = ProductionLineRunStatusMap.FromDb(reader.GetString(6)),
    };

    private static FaultRecord MapFault(OracleDataReader reader) => new()
    {
        FaultId = Convert.ToInt64(reader.GetValue(0)),
        LineId = Convert.ToInt64(reader.GetValue(1)),
        FaultType = reader.GetString(2),
        Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
        OccurTime = reader.GetUtcDateTime(4),
        RecoverTime = reader.IsDBNull(5) ? null : reader.GetUtcDateTime(5),
        Status = FaultStatusMap.FromDb(reader.GetString(6)),
        ReporterId = Convert.ToInt64(reader.GetValue(7)),
        RepairerId = reader.IsDBNull(8) ? null : Convert.ToInt64(reader.GetValue(8)),
    };

    private static ProductionLineStatus MapLineStatus(OracleDataReader reader) => new()
    {
        LineId = Convert.ToInt64(reader.GetValue(0)),
        Status = ProductionLineRunStatusMap.FromDb(reader.GetString(1)),
        CurrentOrderId = reader.IsDBNull(2) ? null : Convert.ToInt64(reader.GetValue(2)),
        CurrentMaterialId = reader.IsDBNull(3) ? null : Convert.ToInt64(reader.GetValue(3)),
        FinishedQty = Convert.ToDecimal(reader.GetValue(4)),
        Efficiency = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
        UpdatedTime = reader.GetUtcDateTime(6),
    };

    private static LineType? GetLineType(OracleConnection connection, long typeId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT TYPE_ID, TYPE_NAME
              FROM LINE_TYPE
              WHERE TYPE_ID = :typeId");
        command.Parameters.Add("typeId", OracleDbType.Int64).Value = typeId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read() ? MapLineType(reader) : null;
    }

    private static ProductionLine? GetLine(
        OracleConnection connection,
        long lineId,
        OracleTransaction? transaction = null)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            LineSelect + " WHERE pl.LINE_ID = :lineId",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read() ? MapLine(reader) : null;
    }

    private static FaultRecord? GetFault(
        OracleConnection connection,
        long faultId,
        OracleTransaction? transaction = null,
        bool forUpdate = false)
    {
        string lockClause = forUpdate ? " FOR UPDATE" : string.Empty;
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT FAULT_ID, LINE_ID, FAULT_TYPE, DESCRIPTION, OCCUR_TIME,
                     RECOVER_TIME, STATUS, REPORTER_ID, REPAIRER_ID
              FROM FAULT_RECORD
              WHERE FAULT_ID = :faultId" + lockClause,
            transaction);
        command.Parameters.Add("faultId", OracleDbType.Int64).Value = faultId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read() ? MapFault(reader) : null;
    }

    private static ProductionLineStatus? GetLineStatus(
        OracleConnection connection,
        long lineId,
        OracleTransaction? transaction = null,
        bool forUpdate = false)
    {
        string lockClause = forUpdate ? " FOR UPDATE" : string.Empty;
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT LINE_ID, STATUS, CURRENT_ORDER_ID, CURRENT_MATERIAL_ID,
                     FINISHED_QTY, EFFICIENCY, UPDATED_TIME
              FROM LINE_STATUS
              WHERE LINE_ID = :lineId" + lockClause,
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read() ? MapLineStatus(reader) : null;
    }

    private static bool LineTypeExists(
        OracleConnection connection,
        long typeId,
        OracleTransaction? transaction = null)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM LINE_TYPE WHERE TYPE_ID = :typeId",
            transaction);
        command.Parameters.Add("typeId", OracleDbType.Int64).Value = typeId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ActiveUserExists(
        OracleConnection connection,
        long userId,
        OracleTransaction? transaction = null)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM SYS_USER
              WHERE USER_ID = :userId
                AND STATUS = 'valid'",
            transaction);
        command.Parameters.Add("userId", OracleDbType.Int64).Value = userId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool LineExists(
        OracleConnection connection,
        long lineId,
        OracleTransaction? transaction = null)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM PRODUCTION_LINE WHERE LINE_ID = :lineId",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static ProductionOrderLineContext? GetProductionOrderLineContext(
        OracleConnection connection,
        OracleTransaction transaction,
        long orderId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT MATERIAL_ID, PLAN_QTY, STATUS
              FROM PRODUCTION_ORDER
              WHERE ORDER_ID = :orderId",
            transaction);
        command.Parameters.Add("orderId", OracleDbType.Int64).Value = orderId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new ProductionOrderLineContext(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToDecimal(reader.GetValue(1)),
                reader.GetString(2))
            : null;
    }

    private static bool HasCalendarTypeConflict(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId,
        long typeId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM PRODUCTION_CALENDAR calendar
              INNER JOIN CAPACITY_CONFIG config ON config.CONFIG_ID = calendar.CONFIG_ID
              WHERE calendar.LINE_ID = :lineId
                AND config.TYPE_ID <> :typeId",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("typeId", OracleDbType.Int64).Value = typeId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static ProductionLineUpdateContext? GetLineUpdateContext(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT TYPE_ID, START_DATE
              FROM PRODUCTION_LINE
              WHERE LINE_ID = :lineId
              FOR UPDATE",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new ProductionLineUpdateContext(
                Convert.ToInt64(reader.GetValue(0)),
                DateOnly.FromDateTime(reader.GetDateTime(1)))
            : null;
    }

    private static bool HasCalendarBeforeStartDate(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId,
        DateOnly startDate)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM PRODUCTION_CALENDAR
              WHERE LINE_ID = :lineId
                AND CALENDAR_DATE < :startDate",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("startDate", OracleDbType.Date).Value =
            startDate.ToDateTime(TimeOnly.MinValue);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool HasActiveFaults(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM FAULT_RECORD
              WHERE LINE_ID = :lineId
                AND STATUS <> :recovered",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("recovered", OracleDbType.Varchar2).Value =
            FaultStatusMap.Db.Recovered;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool MaterialExists(
        OracleConnection connection,
        OracleTransaction transaction,
        long materialId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM MATERIAL WHERE MATERIAL_ID = :materialId",
            transaction);
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void InsertOutputRecord(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId,
        long? orderId,
        decimal outputQty,
        long operatorId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"INSERT INTO LINE_OUTPUT_RECORD
              (LINE_ID, ORDER_ID, OUTPUT_QTY, RECORDED_TIME, OPERATOR_ID)
              VALUES (:lineId, :orderId, :outputQty, SYS_EXTRACT_UTC(SYSTIMESTAMP), :operatorId)",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("orderId", OracleDbType.Int64).Value =
            OracleCommandFactory.DbValue(orderId);
        command.Parameters.Add("outputQty", OracleDbType.Decimal).Value = outputQty;
        command.Parameters.Add("operatorId", OracleDbType.Int64).Value = operatorId;
        command.ExecuteNonQuery();
    }

    private static void UpsertFaultLineStatus(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"MERGE INTO LINE_STATUS target
              USING (SELECT :lineId AS LINE_ID FROM DUAL) source
              ON (target.LINE_ID = source.LINE_ID)
              WHEN MATCHED THEN UPDATE SET
                  target.STATUS = :status,
                  target.UPDATED_TIME = SYS_EXTRACT_UTC(SYSTIMESTAMP)
              WHEN NOT MATCHED THEN INSERT
                  (LINE_ID, STATUS, CURRENT_ORDER_ID, CURRENT_MATERIAL_ID,
                   FINISHED_QTY, EFFICIENCY, UPDATED_TIME)
              VALUES
                  (:lineId, :status, NULL, NULL, 0, 0, SYS_EXTRACT_UTC(SYSTIMESTAMP))",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("status", OracleDbType.Varchar2).Value =
            ProductionLineRunStatusMap.Db.Fault;
        command.ExecuteNonQuery();
    }

    private static int CountActiveFaults(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId,
        long excludedFaultId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM FAULT_RECORD
              WHERE LINE_ID = :lineId
                AND FAULT_ID <> :faultId
                AND STATUS <> :recovered",
            transaction);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("faultId", OracleDbType.Int64).Value = excludedFaultId;
        command.Parameters.Add("recovered", OracleDbType.Varchar2).Value =
            FaultStatusMap.Db.Recovered;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void RestoreIdleLineStatus(
        OracleConnection connection,
        OracleTransaction transaction,
        long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"UPDATE LINE_STATUS
              SET STATUS = :status,
                  CURRENT_ORDER_ID = NULL,
                  CURRENT_MATERIAL_ID = NULL,
                  EFFICIENCY = 0,
                  UPDATED_TIME = SYS_EXTRACT_UTC(SYSTIMESTAMP)
              WHERE LINE_ID = :lineId",
            transaction);
        command.Parameters.Add("status", OracleDbType.Varchar2).Value =
            ProductionLineRunStatusMap.Db.Idle;
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.ExecuteNonQuery();
    }

    private sealed record ProductionOrderLineContext(
        long MaterialId,
        decimal PlanQty,
        string Status);

    private sealed record ProductionLineUpdateContext(long TypeId, DateOnly StartDate);
}
