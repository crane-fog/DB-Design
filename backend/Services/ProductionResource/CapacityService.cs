using System.Data;

using Newtonsoft.Json;

using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed class CapacityService(
    string connString,
    CapacityEstimationDataSource estimationDataSource) : ICapacityService
{
    private const int DuplicateKeyError = 1;
    private const decimal StandardWorkMinutesPerCalendarDay = 480m;
    private const int EstimateHorizonDays = 365;
    private static readonly TimeOnly StandardShiftStart = new(8, 0);

    public ProductionResourceResult<ProductionResourcePage<CapacityConfig>> ListConfigs(
        int page,
        int pageSize,
        long? materialId,
        long? typeId)
    {
        try
        {
            using OracleConnection connection = OpenConnection();
            List<string> filters = [];
            if (materialId.HasValue)
            {
                filters.Add("cc.MATERIAL_ID = :materialId");
            }

            if (typeId.HasValue)
            {
                filters.Add("cc.TYPE_ID = :typeId");
            }

            string whereClause = filters.Count == 0
                ? string.Empty
                : " WHERE " + string.Join(" AND ", filters);

            int total;
            using (OracleCommand countCommand = OracleCommandFactory.Create(
                       connection,
                       "SELECT COUNT(*) FROM CAPACITY_CONFIG cc" + whereClause))
            {
                AddConfigFilters(countCommand, materialId, typeId);
                total = Convert.ToInt32(countCommand.ExecuteScalar());
            }

            List<CapacityConfig> records = [];
            using (OracleCommand command = OracleCommandFactory.Create(
                       connection,
                       CapacityConfigSelect + whereClause +
                       @" ORDER BY cc.CONFIG_ID
                          OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY"))
            {
                AddConfigFilters(command, materialId, typeId);
                command.Parameters.Add("skip", OracleDbType.Int32).Value = (page - 1) * pageSize;
                command.Parameters.Add("take", OracleDbType.Int32).Value = pageSize;

                using OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(MapCapacityConfig(reader));
                }
            }

            return ProductionResourceResult<ProductionResourcePage<CapacityConfig>>.Success(
                new ProductionResourcePage<CapacityConfig>(records, total, page, pageSize),
                "查询成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionResourcePage<CapacityConfig>>.Fail(
                500,
                "查询产能配置失败");
        }
    }

    public ProductionResourceResult<CapacityConfig> SaveConfig(CapacityConfigSaveRequest request)
    {
        if (request.MaterialId <= 0 || request.TypeId <= 0 || request.UnitTime <= 0)
        {
            return ProductionResourceResult<CapacityConfig>.Fail(400, "产能配置参数无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            if (!MaterialExists(connection, request.MaterialId))
            {
                return ProductionResourceResult<CapacityConfig>.Fail(404, "产品不存在");
            }

            if (!LineTypeExists(connection, request.TypeId))
            {
                return ProductionResourceResult<CapacityConfig>.Fail(404, "生产线类型不存在");
            }

            using OracleTransaction transaction = connection.BeginTransaction();
            long configId;
            if (request.ConfigId is > 0)
            {
                CapacityConfigKey? current = ReadCapacityConfigKey(
                    connection,
                    request.ConfigId.Value,
                    transaction);
                if (current is null)
                {
                    return ProductionResourceResult<CapacityConfig>.Fail(404, "产能配置不存在");
                }

                if ((current.MaterialId != request.MaterialId || current.TypeId != request.TypeId)
                    && CapacityConfigHasCalendarReferences(
                        connection,
                        request.ConfigId.Value,
                        transaction))
                {
                    return ProductionResourceResult<CapacityConfig>.Fail(
                        409,
                        "已被生产日历引用的产能配置不能修改产品或生产线类型");
                }

                using OracleCommand command = OracleCommandFactory.Create(
                    connection,
                    @"UPDATE CAPACITY_CONFIG
                      SET MATERIAL_ID = :materialId,
                          TYPE_ID = :typeId,
                          UNIT_TIME = :unitTime
                      WHERE CONFIG_ID = :configId",
                    transaction);
                command.Parameters.Add("materialId", OracleDbType.Int64).Value = request.MaterialId;
                command.Parameters.Add("typeId", OracleDbType.Int64).Value = request.TypeId;
                command.Parameters.Add("unitTime", OracleDbType.Decimal).Value = request.UnitTime;
                command.Parameters.Add("configId", OracleDbType.Int64).Value = request.ConfigId.Value;
                command.ExecuteNonQuery();

                configId = request.ConfigId.Value;
            }
            else
            {
                using OracleCommand command = OracleCommandFactory.Create(
                    connection,
                    @"INSERT INTO CAPACITY_CONFIG
                      (MATERIAL_ID, TYPE_ID, UNIT_TIME)
                      VALUES (:materialId, :typeId, :unitTime)
                      RETURNING CONFIG_ID INTO :newId",
                    transaction);
                command.Parameters.Add("materialId", OracleDbType.Int64).Value = request.MaterialId;
                command.Parameters.Add("typeId", OracleDbType.Int64).Value = request.TypeId;
                command.Parameters.Add("unitTime", OracleDbType.Decimal).Value = request.UnitTime;
                OracleParameter idParameter = new("newId", OracleDbType.Int64)
                {
                    Direction = ParameterDirection.Output,
                };
                command.Parameters.Add(idParameter);
                command.ExecuteNonQuery();
                configId = OracleCommandFactory.ReadIdentity(idParameter);
            }

            CapacityConfig? result = GetCapacityConfig(connection, configId, transaction);
            if (result is null)
            {
                transaction.Rollback();
                return ProductionResourceResult<CapacityConfig>.Fail(
                    500,
                    "保存后无法读取产能配置");
            }

            transaction.Commit();
            return ProductionResourceResult<CapacityConfig>.Success(result, "保存成功");
        }
        catch (OracleException exception) when (exception.Number == DuplicateKeyError)
        {
            return ProductionResourceResult<CapacityConfig>.Fail(409, "产品与生产线类型的产能配置已存在");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<CapacityConfig>.Fail(500, "保存产能配置失败");
        }
    }

    public ProductionResourceResult<ProductionResourcePage<ProductionCalendar>> ListCalendars(
        int page,
        int pageSize,
        long? lineId,
        DateOnly? startDate,
        DateOnly? endDate,
        long? configId)
    {
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return ProductionResourceResult<ProductionResourcePage<ProductionCalendar>>.Fail(
                400,
                "开始日期不得晚于结束日期");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            List<string> filters = [];
            if (lineId.HasValue)
            {
                filters.Add("pc.LINE_ID = :lineId");
            }

            if (startDate.HasValue)
            {
                filters.Add("pc.CALENDAR_DATE >= :startDate");
            }

            if (endDate.HasValue)
            {
                filters.Add("pc.CALENDAR_DATE < :endDateExclusive");
            }

            if (configId.HasValue)
            {
                filters.Add("pc.CONFIG_ID = :configId");
            }

            string whereClause = filters.Count == 0
                ? string.Empty
                : " WHERE " + string.Join(" AND ", filters);

            int total;
            using (OracleCommand countCommand = OracleCommandFactory.Create(
                       connection,
                       "SELECT COUNT(*) FROM PRODUCTION_CALENDAR pc" + whereClause))
            {
                AddCalendarFilters(countCommand, lineId, startDate, endDate, configId);
                total = Convert.ToInt32(countCommand.ExecuteScalar());
            }

            List<ProductionCalendar> records = [];
            using (OracleCommand command = OracleCommandFactory.Create(
                       connection,
                       ProductionCalendarSelect + whereClause +
                       @" ORDER BY pc.CALENDAR_DATE, pc.LINE_ID
                          OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY"))
            {
                AddCalendarFilters(command, lineId, startDate, endDate, configId);
                command.Parameters.Add("skip", OracleDbType.Int32).Value = (page - 1) * pageSize;
                command.Parameters.Add("take", OracleDbType.Int32).Value = pageSize;
                using OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(MapProductionCalendar(reader));
                }
            }

            return ProductionResourceResult<ProductionResourcePage<ProductionCalendar>>.Success(
                new ProductionResourcePage<ProductionCalendar>(records, total, page, pageSize),
                "查询成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionResourcePage<ProductionCalendar>>.Fail(
                500,
                "查询生产日历失败");
        }
    }

    public ProductionResourceResult<ProductionCalendar> SaveCalendar(
        ProductionCalendarSaveRequest request)
    {
        if (request.CalendarDate == default || request.LineId <= 0 || request.ConfigId <= 0)
        {
            return ProductionResourceResult<ProductionCalendar>.Fail(400, "生产日历参数无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            LineCalendarContext? line = ReadLineCalendarContext(connection, request.LineId);
            if (line is null)
            {
                return ProductionResourceResult<ProductionCalendar>.Fail(404, "生产线不存在");
            }

            if (request.CalendarDate < line.StartDate)
            {
                return ProductionResourceResult<ProductionCalendar>.Fail(
                    400,
                    "排产日期不得早于生产线启用日期");
            }

            long? configTypeId = ReadConfigTypeId(connection, request.ConfigId);
            if (!configTypeId.HasValue)
            {
                return ProductionResourceResult<ProductionCalendar>.Fail(404, "产能配置不存在");
            }

            if (line.TypeId != configTypeId)
            {
                return ProductionResourceResult<ProductionCalendar>.Fail(
                    400,
                    "生产线类型与产能配置不匹配");
            }

            using OracleCommand command = OracleCommandFactory.Create(
                connection,
                @"MERGE INTO PRODUCTION_CALENDAR target
                  USING (
                      SELECT :calendarDate AS CALENDAR_DATE,
                             :lineId AS LINE_ID,
                             :configId AS CONFIG_ID
                      FROM DUAL
                  ) source
                  ON (
                      target.CALENDAR_DATE = source.CALENDAR_DATE
                      AND target.LINE_ID = source.LINE_ID
                  )
                  WHEN MATCHED THEN UPDATE SET
                      target.CONFIG_ID = source.CONFIG_ID
                  WHEN NOT MATCHED THEN INSERT
                      (CALENDAR_DATE, LINE_ID, CONFIG_ID)
                  VALUES
                      (source.CALENDAR_DATE, source.LINE_ID, source.CONFIG_ID)");
            command.Parameters.Add("calendarDate", OracleDbType.Date).Value =
                request.CalendarDate.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add("lineId", OracleDbType.Int64).Value = request.LineId;
            command.Parameters.Add("configId", OracleDbType.Int64).Value = request.ConfigId;
            command.ExecuteNonQuery();

            ProductionCalendar? result = GetProductionCalendar(
                connection,
                request.CalendarDate,
                request.LineId);
            return result is null
                ? ProductionResourceResult<ProductionCalendar>.Fail(500, "保存后无法读取生产日历")
                : ProductionResourceResult<ProductionCalendar>.Success(result, "保存成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionCalendar>.Fail(500, "保存生产日历失败");
        }
    }

    public ProductionResourceResult<object> DeleteCalendar(ProductionCalendarDeleteRequest request)
    {
        if (request.CalendarDate == default || request.LineId <= 0)
        {
            return ProductionResourceResult<object>.Fail(400, "生产日历参数无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            using OracleCommand command = OracleCommandFactory.Create(
                connection,
                @"DELETE FROM PRODUCTION_CALENDAR
                  WHERE CALENDAR_DATE = :calendarDate
                    AND LINE_ID = :lineId");
            command.Parameters.Add("calendarDate", OracleDbType.Date).Value =
                request.CalendarDate.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add("lineId", OracleDbType.Int64).Value = request.LineId;
            if (command.ExecuteNonQuery() == 0)
            {
                return ProductionResourceResult<object>.Fail(404, "生产日历不存在");
            }

            return ProductionResourceResult<object>.Success(
                new
                {
                    request.CalendarDate,
                    request.LineId,
                },
                "删除成功");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<object>.Fail(500, "删除生产日历失败");
        }
    }

    public ProductionResourceResult<ProductionCapacityEstimateResult> Estimate(
        ProductionCapacityEstimateRequest request)
    {
        ProductionResourceResult<CapacityEstimateInput> inputResult =
            estimationDataSource.ResolveInput(request);
        if (!inputResult.Ok || inputResult.Data is null)
        {
            return ProductionResourceResult<ProductionCapacityEstimateResult>.Fail(
                inputResult.Code,
                inputResult.Message);
        }

        ProductionResourceResult<MaterialReadiness> materialResult =
            estimationDataSource.EvaluateMaterialReadiness(inputResult.Data);
        if (!materialResult.Ok || materialResult.Data is null)
        {
            return ProductionResourceResult<ProductionCapacityEstimateResult>.Fail(
                materialResult.Code,
                materialResult.Message);
        }

        try
        {
            CapacityEstimateInput input = inputResult.Data;
            MaterialReadiness material = materialResult.Data;
            DateOnly today = BusinessTime.Today;
            DateOnly capacityStart = material.ReadyDate ?? today;
            DateOnly horizon = (input.ExpectedDate > capacityStart
                    ? input.ExpectedDate
                    : capacityStart)
                .AddDays(EstimateHorizonDays);

            using OracleConnection connection = OpenConnection();
            List<CapacityPlan> plans = ReadCapacityPlans(
                connection,
                input.MaterialId,
                capacityStart,
                horizon);
            if (plans.Count == 0)
            {
                return ProductionResourceResult<ProductionCapacityEstimateResult>.Fail(
                    404,
                    "产品尚未配置可用产能");
            }

            CapacityPlanResult selected = AggregateRequiredCapacityPlans(
                plans.Select(plan => EvaluateCapacityPlan(plan, input, capacityStart)),
                capacityStart);

            bool canDeliverOnTime = material.Ready
                && selected.CapacityReady
                && selected.EstimatedFinishDate <= input.ExpectedDate;
            List<string> risks = [];
            if (!material.Ready)
            {
                risks.Add(material.Reason ?? "物料无法按期齐套");
            }

            if (!selected.CapacityReady)
            {
                risks.Add("现有生产日历产能不足");
            }
            else if (selected.EstimatedFinishDate > input.ExpectedDate)
            {
                risks.Add("预计完工日期晚于期望日期");
            }

            ProductionCapacityEstimateResult result = new()
            {
                CanDeliverOnTime = canDeliverOnTime,
                MaterialReady = material.Ready,
                CapacityReady = selected.CapacityReady,
                LatestMaterialReadyDate = material.ReadyDate,
                EstimatedFinishDate = selected.EstimatedFinishDate,
                RequiredWorkMinutes = selected.RequiredMinutes,
                AvailableWorkMinutes = selected.AvailableBeforeExpected,
                RiskReason = risks.Count == 0 ? string.Empty : string.Join("；", risks),
            };
            return ProductionResourceResult<ProductionCapacityEstimateResult>.Success(
                result,
                "估算完成");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<ProductionCapacityEstimateResult>.Fail(
                500,
                "估算生产产能失败");
        }
    }

    public ProductionResourceResult<CapacityDetection> RunDetection(
        CapacityDetectionRunRequest request)
    {
        if (request.LineId <= 0
            || request.PeriodStart == default
            || request.PeriodEnd == default
            || request.PeriodEnd <= request.PeriodStart)
        {
            return ProductionResourceResult<CapacityDetection>.Fail(400, "产能检测周期无效");
        }

        try
        {
            using OracleConnection connection = OpenConnection();
            if (!LineExists(connection, request.LineId))
            {
                return ProductionResourceResult<CapacityDetection>.Fail(404, "生产线不存在");
            }

            List<ScheduledCapacity> schedules = ReadDetectionSchedules(
                connection,
                request.LineId,
                request.PeriodStart,
                request.PeriodEnd);
            decimal plannedWorkMinutes = 0;
            decimal planCapacity = 0;
            List<WorkInterval> plannedWorkIntervals = [];
            foreach (ScheduledCapacity schedule in schedules)
            {
                DateTime workStart = BusinessTime.ToUtc(schedule.Date, StandardShiftStart);
                DateTime workEnd = workStart.AddMinutes((double)StandardWorkMinutesPerCalendarDay);
                decimal overlapMinutes = CalculateOverlapMinutes(
                    request.PeriodStart,
                    request.PeriodEnd,
                    workStart,
                    workEnd);
                plannedWorkMinutes += overlapMinutes;
                planCapacity += overlapMinutes / schedule.UnitTime;
                WorkInterval? overlap = CalculateOverlapInterval(
                    request.PeriodStart,
                    request.PeriodEnd,
                    workStart,
                    workEnd);
                if (overlap is not null)
                {
                    plannedWorkIntervals.Add(overlap);
                }
            }

            planCapacity = decimal.Round(planCapacity, 2, MidpointRounding.AwayFromZero);
            decimal downtimeMinutes = ReadDowntimeMinutes(
                connection,
                request.LineId,
                request.PeriodStart,
                request.PeriodEnd,
                plannedWorkIntervals);
            decimal actualCapacity = decimal.Round(
                ReadActualCapacity(
                    connection,
                    request.LineId,
                    request.PeriodStart,
                    request.PeriodEnd),
                2,
                MidpointRounding.AwayFromZero);
            decimal actualWorkHours = Math.Max(0, plannedWorkMinutes - downtimeMinutes) / 60m;
            decimal diffQuantity = decimal.Round(
                actualCapacity - planCapacity,
                2,
                MidpointRounding.AwayFromZero);
            decimal diffRate = planCapacity == 0
                ? 0
                : Math.Clamp(
                    decimal.Round(diffQuantity / planCapacity, 4),
                    -9.9999m,
                    9.9999m);
            decimal? efficiency = planCapacity == 0
                ? null
                : Math.Clamp(
                    decimal.Round(actualCapacity / planCapacity, 4),
                    0m,
                    1m);
            string reasonType = downtimeMinutes > 0
                ? "故障停机"
                : actualCapacity < planCapacity
                    ? "产能不足"
                    : "正常";

            long detectionId;
            using (OracleCommand command = OracleCommandFactory.Create(
                       connection,
                       @"INSERT INTO CAPACITY_DETECTION
                         (LINE_ID, PERIOD_START, PERIOD_END, PLAN_CAPACITY,
                          ACTUAL_CAPACITY, DIFF_QTY, DIFF_RATE, REASON_TYPE)
                         VALUES
                         (:lineId, :periodStart, :periodEnd, :planCapacity,
                          :actualCapacity, :diffQty, :diffRate, :reasonType)
                         RETURNING DETECTION_ID INTO :newId"))
            {
                command.Parameters.Add("lineId", OracleDbType.Int64).Value = request.LineId;
                command.Parameters.Add("periodStart", OracleDbType.TimeStamp).Value =
                    request.PeriodStart;
                command.Parameters.Add("periodEnd", OracleDbType.TimeStamp).Value = request.PeriodEnd;
                command.Parameters.Add("planCapacity", OracleDbType.Decimal).Value = planCapacity;
                command.Parameters.Add("actualCapacity", OracleDbType.Decimal).Value = actualCapacity;
                command.Parameters.Add("diffQty", OracleDbType.Decimal).Value = diffQuantity;
                command.Parameters.Add("diffRate", OracleDbType.Decimal).Value = diffRate;
                command.Parameters.Add("reasonType", OracleDbType.Varchar2).Value = reasonType;
                OracleParameter idParameter = new("newId", OracleDbType.Int64)
                {
                    Direction = ParameterDirection.Output,
                };
                command.Parameters.Add(idParameter);
                command.ExecuteNonQuery();
                detectionId = OracleCommandFactory.ReadIdentity(idParameter);
            }

            CapacityDetection result = new()
            {
                DetectionId = detectionId,
                LineId = request.LineId,
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                PlanCapacity = planCapacity,
                ActualCapacity = actualCapacity,
                ActualWorkHours = actualWorkHours,
                DowntimeMinutes = downtimeMinutes,
                Efficiency = efficiency,
                DiffQty = diffQuantity,
                DiffRate = diffRate,
                ReasonType = reasonType,
            };
            return ProductionResourceResult<CapacityDetection>.Success(result, "产能检测完成");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<CapacityDetection>.Fail(500, "执行产能检测失败");
        }
    }

    public ProductionResourceResult<CapacityBalance> SaveBalance(
        CapacityBalanceSaveRequest request,
        CurrentUser currentUser)
    {
        if (request.BeforePlan is null
            || request.BeforePlan.Count == 0
            || request.AfterPlan is null
            || request.AfterPlan.Count == 0
            || request.AffectedOrders is null
            || request.AffectedOrders.Count == 0
            || request.AffectedOrders.Any(orderId => orderId <= 0)
            || request.AffectedOrders.Count > 1000)
        {
            return ProductionResourceResult<CapacityBalance>.Fail(400, "产能平衡方案参数无效");
        }

        List<long> affectedOrders = request.AffectedOrders.Distinct().ToList();
        try
        {
            using OracleConnection connection = OpenConnection();
            if (!AllOrdersExist(connection, affectedOrders))
            {
                return ProductionResourceResult<CapacityBalance>.Fail(404, "受影响生产订单不存在");
            }

            string beforePlan = JsonConvert.SerializeObject(request.BeforePlan);
            string afterPlan = JsonConvert.SerializeObject(request.AfterPlan);
            string affectedOrderJson = JsonConvert.SerializeObject(affectedOrders);

            long balanceId;
            using (OracleCommand command = OracleCommandFactory.Create(
                       connection,
                       @"INSERT INTO CAPACITY_BALANCE
                         (BEFORE_PLAN, AFTER_PLAN, OPERATOR_ID, ADJUST_TIME, AFFECTED_ORDERS)
                         VALUES
                         (:beforePlan, :afterPlan, :operatorId, SYS_EXTRACT_UTC(SYSTIMESTAMP), :affectedOrders)
                         RETURNING BALANCE_ID INTO :newId"))
            {
                command.Parameters.Add("beforePlan", OracleDbType.Clob).Value = beforePlan;
                command.Parameters.Add("afterPlan", OracleDbType.Clob).Value = afterPlan;
                command.Parameters.Add("operatorId", OracleDbType.Int64).Value = currentUser.UserId;
                command.Parameters.Add("affectedOrders", OracleDbType.Clob).Value = affectedOrderJson;
                OracleParameter idParameter = new("newId", OracleDbType.Int64)
                {
                    Direction = ParameterDirection.Output,
                };
                command.Parameters.Add(idParameter);
                command.ExecuteNonQuery();
                balanceId = OracleCommandFactory.ReadIdentity(idParameter);
            }

            CapacityBalance? result = GetCapacityBalance(connection, balanceId);
            return result is null
                ? ProductionResourceResult<CapacityBalance>.Fail(500, "保存后无法读取产能平衡方案")
                : ProductionResourceResult<CapacityBalance>.Success(result, "保存成功");
        }
        catch (JsonException)
        {
            return ProductionResourceResult<CapacityBalance>.Fail(400, "产能平衡方案无法序列化");
        }
        catch (OracleException)
        {
            return ProductionResourceResult<CapacityBalance>.Fail(500, "保存产能平衡方案失败");
        }
    }

    private const string CapacityConfigSelect = @"
        SELECT cc.CONFIG_ID,
               cc.MATERIAL_ID,
               m.MATERIAL_NAME,
               cc.TYPE_ID,
               lt.TYPE_NAME,
               cc.UNIT_TIME
        FROM CAPACITY_CONFIG cc
        JOIN MATERIAL m ON m.MATERIAL_ID = cc.MATERIAL_ID
        JOIN LINE_TYPE lt ON lt.TYPE_ID = cc.TYPE_ID";

    private const string ProductionCalendarSelect = @"
        SELECT pc.CALENDAR_DATE,
               pc.LINE_ID,
               lt.TYPE_NAME || '-' || TO_CHAR(pc.LINE_ID),
               pc.CONFIG_ID,
               cc.MATERIAL_ID,
               m.MATERIAL_NAME,
               cc.TYPE_ID,
               lt.TYPE_NAME
        FROM PRODUCTION_CALENDAR pc
        JOIN PRODUCTION_LINE pl ON pl.LINE_ID = pc.LINE_ID
        JOIN CAPACITY_CONFIG cc ON cc.CONFIG_ID = pc.CONFIG_ID
        JOIN MATERIAL m ON m.MATERIAL_ID = cc.MATERIAL_ID
        JOIN LINE_TYPE lt ON lt.TYPE_ID = cc.TYPE_ID";

    private OracleConnection OpenConnection()
    {
        OracleConnection connection = new(connString);
        connection.Open();
        return connection;
    }

    private static void AddConfigFilters(
        OracleCommand command,
        long? materialId,
        long? typeId)
    {
        if (materialId.HasValue)
        {
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId.Value;
        }

        if (typeId.HasValue)
        {
            command.Parameters.Add("typeId", OracleDbType.Int64).Value = typeId.Value;
        }
    }

    private static void AddCalendarFilters(
        OracleCommand command,
        long? lineId,
        DateOnly? startDate,
        DateOnly? endDate,
        long? configId)
    {
        if (lineId.HasValue)
        {
            command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId.Value;
        }

        if (startDate.HasValue)
        {
            command.Parameters.Add("startDate", OracleDbType.Date).Value =
                startDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (endDate.HasValue)
        {
            command.Parameters.Add("endDateExclusive", OracleDbType.Date).Value =
                endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
        }

        if (configId.HasValue)
        {
            command.Parameters.Add("configId", OracleDbType.Int64).Value = configId.Value;
        }
    }

    private static CapacityConfig MapCapacityConfig(OracleDataReader reader) => new()
    {
        ConfigId = Convert.ToInt64(reader.GetValue(0)),
        MaterialId = Convert.ToInt64(reader.GetValue(1)),
        MaterialName = reader.GetString(2),
        TypeId = Convert.ToInt64(reader.GetValue(3)),
        TypeName = reader.GetString(4),
        UnitTime = Convert.ToDecimal(reader.GetValue(5)),
    };

    private static ProductionCalendar MapProductionCalendar(OracleDataReader reader) => new()
    {
        CalendarDate = DateOnly.FromDateTime(reader.GetDateTime(0)),
        LineId = Convert.ToInt64(reader.GetValue(1)),
        LineName = reader.GetString(2),
        ConfigId = Convert.ToInt64(reader.GetValue(3)),
        MaterialId = Convert.ToInt64(reader.GetValue(4)),
        MaterialName = reader.GetString(5),
        TypeId = Convert.ToInt64(reader.GetValue(6)),
        TypeName = reader.GetString(7),
    };

    private static CapacityConfig? GetCapacityConfig(
        OracleConnection connection,
        long configId,
        OracleTransaction? transaction = null)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            CapacityConfigSelect + " WHERE cc.CONFIG_ID = :configId",
            transaction);
        command.Parameters.Add("configId", OracleDbType.Int64).Value = configId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read() ? MapCapacityConfig(reader) : null;
    }

    private static ProductionCalendar? GetProductionCalendar(
        OracleConnection connection,
        DateOnly calendarDate,
        long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            ProductionCalendarSelect +
            @" WHERE pc.CALENDAR_DATE = :calendarDate
                AND pc.LINE_ID = :lineId");
        command.Parameters.Add("calendarDate", OracleDbType.Date).Value =
            calendarDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read() ? MapProductionCalendar(reader) : null;
    }

    private static bool MaterialExists(OracleConnection connection, long materialId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM MATERIAL WHERE MATERIAL_ID = :materialId");
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool LineTypeExists(OracleConnection connection, long typeId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM LINE_TYPE WHERE TYPE_ID = :typeId");
        command.Parameters.Add("typeId", OracleDbType.Int64).Value = typeId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool LineExists(OracleConnection connection, long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT COUNT(*) FROM PRODUCTION_LINE WHERE LINE_ID = :lineId");
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static LineCalendarContext? ReadLineCalendarContext(
        OracleConnection connection,
        long lineId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT TYPE_ID, START_DATE FROM PRODUCTION_LINE WHERE LINE_ID = :lineId");
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new LineCalendarContext(
                Convert.ToInt64(reader.GetValue(0)),
                DateOnly.FromDateTime(reader.GetDateTime(1)))
            : null;
    }

    private static CapacityConfigKey? ReadCapacityConfigKey(
        OracleConnection connection,
        long configId,
        OracleTransaction transaction)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT MATERIAL_ID, TYPE_ID
              FROM CAPACITY_CONFIG
              WHERE CONFIG_ID = :configId
              FOR UPDATE",
            transaction);
        command.Parameters.Add("configId", OracleDbType.Int64).Value = configId;
        using OracleDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new CapacityConfigKey(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToInt64(reader.GetValue(1)))
            : null;
    }

    private static bool CapacityConfigHasCalendarReferences(
        OracleConnection connection,
        long configId,
        OracleTransaction transaction)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT COUNT(*)
              FROM PRODUCTION_CALENDAR
              WHERE CONFIG_ID = :configId",
            transaction);
        command.Parameters.Add("configId", OracleDbType.Int64).Value = configId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static long? ReadConfigTypeId(OracleConnection connection, long configId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            "SELECT TYPE_ID FROM CAPACITY_CONFIG WHERE CONFIG_ID = :configId");
        command.Parameters.Add("configId", OracleDbType.Int64).Value = configId;
        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static List<CapacityPlan> ReadCapacityPlans(
        OracleConnection connection,
        long materialId,
        DateOnly startDate,
        DateOnly horizon)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT cc.CONFIG_ID, cc.TYPE_ID, cc.UNIT_TIME,
                     pc.CALENDAR_DATE, pl.LINE_ID
              FROM CAPACITY_CONFIG cc
              LEFT JOIN PRODUCTION_CALENDAR pc
                ON pc.CONFIG_ID = cc.CONFIG_ID
               AND pc.CALENDAR_DATE >= :startDate
               AND pc.CALENDAR_DATE <= :horizon
              LEFT JOIN PRODUCTION_LINE pl
                ON pl.LINE_ID = pc.LINE_ID
               AND pl.TYPE_ID = cc.TYPE_ID
              WHERE cc.MATERIAL_ID = :materialId
              ORDER BY cc.UNIT_TIME, cc.CONFIG_ID, pc.CALENDAR_DATE, pc.LINE_ID");
        command.Parameters.Add("startDate", OracleDbType.Date).Value =
            startDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("horizon", OracleDbType.Date).Value =
            horizon.ToDateTime(TimeOnly.MaxValue);
        command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;

        List<CapacityPlan> plans = [];
        var plansByConfig = new Dictionary<long, CapacityPlan>();
        using (OracleDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                long configId = Convert.ToInt64(reader.GetValue(0));
                if (!plansByConfig.TryGetValue(configId, out CapacityPlan? plan))
                {
                    plan = new CapacityPlan(
                        configId,
                        Convert.ToInt64(reader.GetValue(1)),
                        Convert.ToDecimal(reader.GetValue(2)),
                        []);
                    plansByConfig.Add(configId, plan);
                    plans.Add(plan);
                }

                if (!reader.IsDBNull(3) && !reader.IsDBNull(4))
                {
                    plan.CalendarSlots.Add(DateOnly.FromDateTime(reader.GetDateTime(3)));
                }
            }
        }

        return plans;
    }

    private static CapacityPlanResult EvaluateCapacityPlan(
        CapacityPlan plan,
        CapacityEstimateInput input,
        DateOnly capacityStart)
    {
        decimal requiredMinutes = input.PlanQty * plan.UnitTime;
        decimal availableBeforeExpected = plan.CalendarSlots.Count(date => date <= input.ExpectedDate)
            * StandardWorkMinutesPerCalendarDay;
        decimal availableWithinHorizon =
            plan.CalendarSlots.Count * StandardWorkMinutesPerCalendarDay;
        decimal accumulated = 0;
        DateOnly estimatedFinishDate = plan.CalendarSlots.Count > 0
            ? plan.CalendarSlots[^1]
            : capacityStart;
        bool completedWithinHorizon = false;

        foreach (DateOnly date in plan.CalendarSlots)
        {
            accumulated += StandardWorkMinutesPerCalendarDay;
            if (accumulated >= requiredMinutes)
            {
                estimatedFinishDate = date;
                completedWithinHorizon = true;
                break;
            }
        }

        bool capacityReady = availableBeforeExpected >= requiredMinutes;
        return new CapacityPlanResult(
            requiredMinutes,
            availableBeforeExpected,
            availableWithinHorizon,
            estimatedFinishDate,
            capacityReady && completedWithinHorizon);
    }

    private static CapacityPlanResult AggregateRequiredCapacityPlans(
        IEnumerable<CapacityPlanResult> results,
        DateOnly capacityStart)
    {
        List<CapacityPlanResult> candidates = [.. results];
        if (candidates.Count == 0)
        {
            return new CapacityPlanResult(0, 0, 0, capacityStart, false);
        }

        return new CapacityPlanResult(
            candidates.Sum(result => result.RequiredMinutes),
            candidates.Sum(result => result.AvailableBeforeExpected),
            candidates.Sum(result => result.AvailableWithinHorizon),
            candidates.Max(result => result.EstimatedFinishDate),
            candidates.All(result => result.CapacityReady));
    }

    private static List<ScheduledCapacity> ReadDetectionSchedules(
        OracleConnection connection,
        long lineId,
        DateTime periodStart,
        DateTime periodEnd)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT pc.CALENDAR_DATE, cc.UNIT_TIME
              FROM PRODUCTION_CALENDAR pc
              JOIN CAPACITY_CONFIG cc ON cc.CONFIG_ID = pc.CONFIG_ID
              WHERE pc.LINE_ID = :lineId
                AND pc.CALENDAR_DATE >= :startDate
                AND pc.CALENDAR_DATE <= :endDate
              ORDER BY pc.CALENDAR_DATE");
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("startDate", OracleDbType.Date).Value =
            BusinessTime.ToDate(periodStart).ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("endDate", OracleDbType.Date).Value =
            BusinessTime.ToDate(periodEnd).ToDateTime(TimeOnly.MinValue);

        List<ScheduledCapacity> result = [];
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ScheduledCapacity(
                DateOnly.FromDateTime(reader.GetDateTime(0)),
                Convert.ToDecimal(reader.GetValue(1))));
        }

        return result;
    }

    private static decimal ReadDowntimeMinutes(
        OracleConnection connection,
        long lineId,
        DateTime periodStart,
        DateTime periodEnd,
        IReadOnlyList<WorkInterval> plannedWorkIntervals)
    {
        if (plannedWorkIntervals.Count == 0)
        {
            return 0;
        }

        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT OCCUR_TIME, RECOVER_TIME
              FROM FAULT_RECORD
              WHERE LINE_ID = :lineId
                AND OCCUR_TIME < :periodEnd
                AND NVL(RECOVER_TIME, SYS_EXTRACT_UTC(SYSTIMESTAMP)) > :periodStart");
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("periodEnd", OracleDbType.TimeStamp).Value = periodEnd;
        command.Parameters.Add("periodStart", OracleDbType.TimeStamp).Value = periodStart;

        List<(DateTime Start, DateTime End)> intervals = [];
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            DateTime occurTime = reader.GetUtcDateTime(0);
            DateTime recoverTime = reader.IsDBNull(1) ? DateTime.UtcNow : reader.GetUtcDateTime(1);
            foreach (WorkInterval workInterval in plannedWorkIntervals)
            {
                WorkInterval? overlap = CalculateOverlapInterval(
                    occurTime,
                    recoverTime,
                    workInterval.Start,
                    workInterval.End);
                if (overlap is not null)
                {
                    intervals.Add((overlap.Start, overlap.End));
                }
            }
        }

        if (intervals.Count == 0)
        {
            return 0;
        }

        intervals.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        DateTime mergedStart = intervals[0].Start;
        DateTime mergedEnd = intervals[0].End;
        decimal totalMinutes = 0;
        foreach ((DateTime start, DateTime end) in intervals.Skip(1))
        {
            if (start <= mergedEnd)
            {
                if (end > mergedEnd)
                {
                    mergedEnd = end;
                }

                continue;
            }

            totalMinutes += (decimal)(mergedEnd - mergedStart).TotalMinutes;
            mergedStart = start;
            mergedEnd = end;
        }

        totalMinutes += (decimal)(mergedEnd - mergedStart).TotalMinutes;
        return totalMinutes;
    }

    private static decimal ReadActualCapacity(
        OracleConnection connection,
        long lineId,
        DateTime periodStart,
        DateTime periodEnd)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT NVL(SUM(OUTPUT_QTY), 0)
              FROM LINE_OUTPUT_RECORD
              WHERE LINE_ID = :lineId
                AND RECORDED_TIME >= :periodStart
                AND RECORDED_TIME < :periodEnd");
        command.Parameters.Add("lineId", OracleDbType.Int64).Value = lineId;
        command.Parameters.Add("periodStart", OracleDbType.TimeStamp).Value = periodStart;
        command.Parameters.Add("periodEnd", OracleDbType.TimeStamp).Value = periodEnd;
        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
    }

    private static WorkInterval? CalculateOverlapInterval(
        DateTime firstStart,
        DateTime firstEnd,
        DateTime secondStart,
        DateTime secondEnd)
    {
        DateTime start = firstStart > secondStart ? firstStart : secondStart;
        DateTime end = firstEnd < secondEnd ? firstEnd : secondEnd;
        return end <= start ? null : new WorkInterval(start, end);
    }

    private static decimal CalculateOverlapMinutes(
        DateTime firstStart,
        DateTime firstEnd,
        DateTime secondStart,
        DateTime secondEnd)
    {
        DateTime start = firstStart > secondStart ? firstStart : secondStart;
        DateTime end = firstEnd < secondEnd ? firstEnd : secondEnd;
        return end <= start ? 0 : (decimal)(end - start).TotalMinutes;
    }

    private static bool AllOrdersExist(OracleConnection connection, IReadOnlyList<long> orderIds)
    {
        List<string> placeholders = [];
        using OracleCommand command = OracleCommandFactory.Create(connection, string.Empty);
        for (int index = 0; index < orderIds.Count; index++)
        {
            string parameterName = $"orderId{index}";
            placeholders.Add($":{parameterName}");
            command.Parameters.Add(parameterName, OracleDbType.Int64).Value = orderIds[index];
        }

        command.CommandText =
            $"SELECT COUNT(*) FROM PRODUCTION_ORDER WHERE ORDER_ID IN ({string.Join(", ", placeholders)})";
        return Convert.ToInt32(command.ExecuteScalar()) == orderIds.Count;
    }

    private static CapacityBalance? GetCapacityBalance(
        OracleConnection connection,
        long balanceId)
    {
        using OracleCommand command = OracleCommandFactory.Create(
            connection,
            @"SELECT BALANCE_ID, BEFORE_PLAN, AFTER_PLAN, OPERATOR_ID,
                     ADJUST_TIME, AFFECTED_ORDERS
              FROM CAPACITY_BALANCE
              WHERE BALANCE_ID = :balanceId");
        command.Parameters.Add("balanceId", OracleDbType.Int64).Value = balanceId;
        using OracleDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        string beforePlan = reader.GetOracleClob(1).Value;
        string afterPlan = reader.GetOracleClob(2).Value;
        string affectedOrders = reader.GetOracleClob(5).Value;
        return new CapacityBalance
        {
            BalanceId = Convert.ToInt64(reader.GetValue(0)),
            BeforePlan = JsonConvert.DeserializeObject<Dictionary<string, object>>(beforePlan) ?? [],
            AfterPlan = JsonConvert.DeserializeObject<Dictionary<string, object>>(afterPlan) ?? [],
            OperatorId = Convert.ToInt64(reader.GetValue(3)),
            AdjustTime = reader.GetUtcDateTime(4),
            AffectedOrders = JsonConvert.DeserializeObject<List<long>>(affectedOrders) ?? [],
        };
    }

    private sealed record CapacityPlan(
        long ConfigId,
        long TypeId,
        decimal UnitTime,
        List<DateOnly> CalendarSlots);

    private sealed record CapacityPlanResult(
        decimal RequiredMinutes,
        decimal AvailableBeforeExpected,
        decimal AvailableWithinHorizon,
        DateOnly EstimatedFinishDate,
        bool CapacityReady);

    private sealed record ScheduledCapacity(DateOnly Date, decimal UnitTime);

    private sealed record LineCalendarContext(long TypeId, DateOnly StartDate);

    private sealed record CapacityConfigKey(long MaterialId, long TypeId);

    private sealed record WorkInterval(DateTime Start, DateTime End);
}
