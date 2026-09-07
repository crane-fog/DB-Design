using System.Collections;
using System.Reflection;

using Backend.Filters;

using Newtonsoft.Json.Linq;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Backend.Services;

/// <summary>为更新、状态流转和删除操作读取数据库中的真实前后状态。</summary>
public sealed class OperationAuditSnapshotService(string connString)
{
    public Dictionary<string, object> Capture(
        OperationAuditSnapshotKind snapshotKind,
        IDictionary<string, object?> actionArguments)
    {
        object request = GetRequest(actionArguments);
        using var connection = new OracleConnection(connString);
        connection.Open();

        return snapshotKind switch
        {
            OperationAuditSnapshotKind.User => CaptureUser(connection, request),
            OperationAuditSnapshotKind.Role => CaptureRole(connection, request),
            OperationAuditSnapshotKind.UserRoles => CaptureUserRoles(connection, request),
            OperationAuditSnapshotKind.RolePermissions => CaptureRolePermissions(connection, request),
            OperationAuditSnapshotKind.MaterialCategory => Single(
                "material_category",
                QueryOne(connection, "SELECT * FROM MATERIAL_CATEGORY WHERE CATEGORY_ID = :id", ("id", GetLong(request, "CategoryId")))),
            OperationAuditSnapshotKind.Material => CaptureMaterial(connection, request),
            OperationAuditSnapshotKind.BomVersion => CaptureBomVersion(connection, request),
            OperationAuditSnapshotKind.Bom => Single(
                "bom",
                QueryOne(connection, "SELECT * FROM BOM WHERE BOM_ID = :id", ("id", GetLong(request, "BomId")))),
            OperationAuditSnapshotKind.ExternalOrder => CaptureExternalOrder(connection, request),
            OperationAuditSnapshotKind.StockAlert => Single(
                "stock_alert",
                QueryOne(connection, "SELECT * FROM STOCK_ALERT WHERE ALERT_ID = :id", ("id", GetLong(request, "AlertId")))),
            OperationAuditSnapshotKind.StockLockByOrder => CaptureStockLockByOrder(connection, request),
            OperationAuditSnapshotKind.StockLockById => CaptureStockLockById(connection, request),
            OperationAuditSnapshotKind.WasteDetection => Single(
                "waste_detection",
                QueryOne(connection, "SELECT * FROM WASTE_DETECTION WHERE DETECTION_ID = :id", ("id", GetLong(request, "DetectionId")))),
            OperationAuditSnapshotKind.ProductionOrder => CaptureProductionOrder(connection, request),
            OperationAuditSnapshotKind.LineType => CaptureLineType(connection, request),
            OperationAuditSnapshotKind.ProductionLine => CaptureProductionLine(connection, request),
            OperationAuditSnapshotKind.CapacityConfig => CaptureCapacityConfig(connection, request),
            OperationAuditSnapshotKind.ProductionCalendar => Single(
                "production_calendar",
                QueryOne(
                    connection,
                    "SELECT * FROM PRODUCTION_CALENDAR WHERE LINE_ID = :lineId AND CALENDAR_DATE = :calendarDate",
                    ("lineId", GetLong(request, "LineId")),
                    ("calendarDate", GetValue(request, "CalendarDate")))),
            OperationAuditSnapshotKind.FaultRecord => CaptureFaultRecord(connection, request),
            OperationAuditSnapshotKind.LineStatus => Single(
                "line_status",
                QueryOne(connection, "SELECT * FROM LINE_STATUS WHERE LINE_ID = :id", ("id", GetLong(request, "LineId")))),
            OperationAuditSnapshotKind.PurchaseOrder => CapturePurchaseOrder(connection, request),
            OperationAuditSnapshotKind.OverdueReminder => Single(
                "overdue_reminder",
                QueryOne(connection, "SELECT * FROM OVERDUE_REMINDER WHERE REMINDER_ID = :id", ("id", GetLong(request, "ReminderId")))),
            OperationAuditSnapshotKind.BatchConsumption => Single(
                "batch_consumption",
                QueryOne(connection, "SELECT * FROM BATCH_CONSUMPTION WHERE CONSUMPTION_ID = :id", ("id", GetLong(request, "ConsumptionId")))),
            _ => throw new InvalidOperationException($"不支持的操作审计快照类型：{snapshotKind}"),
        };
    }

    private static Dictionary<string, object> CaptureUser(OracleConnection connection, object request)
    {
        long userId = GetLong(request, "UserId");
        return new Dictionary<string, object>
        {
            ["user"] = QueryOne(
                connection,
                @"SELECT USER_ID, EMPLOYEE_NO, USER_NAME, PHONE, EMAIL, STATUS,
                         CREATED_TIME, LAST_LOGIN_TIME, PWD_UPDATE_TIME
                  FROM SYS_USER WHERE USER_ID = :id",
                ("id", userId))!,
            ["roles"] = QueryRows(
                connection,
                @"SELECT UR.USER_ID, UR.ROLE_ID, R.ROLE_NAME, R.STATUS ROLE_STATUS
                  FROM SYS_USER_ROLE UR
                  JOIN SYS_ROLE R ON R.ROLE_ID = UR.ROLE_ID
                  WHERE UR.USER_ID = :id
                  ORDER BY UR.ROLE_ID",
                ("id", userId)),
        };
    }

    private static Dictionary<string, object> CaptureRole(OracleConnection connection, object request)
    {
        long roleId = GetLong(request, "RoleId");
        return new Dictionary<string, object>
        {
            ["role"] = QueryOne(connection, "SELECT * FROM SYS_ROLE WHERE ROLE_ID = :id", ("id", roleId))!,
            ["permissions"] = QueryRows(
                connection,
                @"SELECT RP.ROLE_ID, RP.PERMISSION_ID, P.PERMISSION_CODE
                  FROM SYS_ROLE_PERMISSION RP
                  JOIN SYS_PERMISSION P ON P.PERMISSION_ID = RP.PERMISSION_ID
                  WHERE RP.ROLE_ID = :id
                  ORDER BY RP.PERMISSION_ID",
                ("id", roleId)),
        };
    }

    private static Dictionary<string, object> CaptureUserRoles(OracleConnection connection, object request)
    {
        long userId = GetLong(request, "UserId");
        return new Dictionary<string, object>
        {
            ["user_id"] = userId,
            ["roles"] = QueryRows(
                connection,
                @"SELECT UR.USER_ID, UR.ROLE_ID, R.ROLE_NAME, R.STATUS ROLE_STATUS
                  FROM SYS_USER_ROLE UR
                  JOIN SYS_ROLE R ON R.ROLE_ID = UR.ROLE_ID
                  WHERE UR.USER_ID = :id
                  ORDER BY UR.ROLE_ID",
                ("id", userId)),
        };
    }

    private static Dictionary<string, object> CaptureRolePermissions(OracleConnection connection, object request)
    {
        long roleId = GetLong(request, "RoleId");
        return new Dictionary<string, object>
        {
            ["role_id"] = roleId,
            ["permissions"] = QueryRows(
                connection,
                @"SELECT RP.ROLE_ID, RP.PERMISSION_ID, P.PERMISSION_CODE,
                         P.MODULE_NAME, P.RESOURCE_NAME, P.ACTION_NAME
                  FROM SYS_ROLE_PERMISSION RP
                  JOIN SYS_PERMISSION P ON P.PERMISSION_ID = RP.PERMISSION_ID
                  WHERE RP.ROLE_ID = :id
                  ORDER BY RP.PERMISSION_ID",
                ("id", roleId)),
        };
    }

    private static Dictionary<string, object> CaptureMaterial(OracleConnection connection, object request)
    {
        long materialId = GetLong(request, "MaterialId");
        return new Dictionary<string, object>
        {
            ["material"] = QueryOne(connection, "SELECT * FROM MATERIAL WHERE MATERIAL_ID = :id", ("id", materialId))!,
            ["stock"] = QueryOne(connection, "SELECT * FROM MATERIAL_STOCK WHERE MATERIAL_ID = :id", ("id", materialId))!,
        };
    }

    private static Dictionary<string, object> CaptureBomVersion(OracleConnection connection, object request)
    {
        long versionId = GetLong(request, "VersionId");
        return new Dictionary<string, object>
        {
            ["version"] = QueryOne(connection, "SELECT * FROM BOM_VERSION WHERE VERSION_ID = :id", ("id", versionId))!,
            ["bom_items"] = QueryRows(connection, "SELECT * FROM BOM WHERE VERSION_ID = :id ORDER BY BOM_ID", ("id", versionId)),
        };
    }

    private static Dictionary<string, object> CaptureExternalOrder(OracleConnection connection, object request)
    {
        long orderId = GetLong(request, "ExtOrderId");
        return new Dictionary<string, object>
        {
            ["external_order"] = QueryOne(connection, "SELECT * FROM EXTERNAL_ORDER WHERE EXT_ORDER_ID = :id", ("id", orderId))!,
            ["production_associations"] = QueryRows(
                connection,
                "SELECT * FROM EXTERNAL_ORDER_PRODUCTION WHERE EXT_ORDER_ID = :id ORDER BY ORDER_ID",
                ("id", orderId)),
            ["production_orders"] = QueryRows(
                connection,
                @"SELECT PO.*
                  FROM PRODUCTION_ORDER PO
                  JOIN EXTERNAL_ORDER_PRODUCTION EOP ON EOP.ORDER_ID = PO.ORDER_ID
                  WHERE EOP.EXT_ORDER_ID = :id
                  ORDER BY PO.ORDER_ID",
                ("id", orderId)),
        };
    }

    private static Dictionary<string, object> CaptureStockLockByOrder(OracleConnection connection, object request)
    {
        long orderId = GetLong(request, "OrderId");
        List<Dictionary<string, object>> locks = QueryRows(
            connection,
            "SELECT * FROM STOCK_LOCK WHERE ORDER_ID = :id ORDER BY LOCK_ID",
            ("id", orderId));
        HashSet<long> materialIds = GetNestedLongs(request, "Items", "MaterialId");
        AddLongValues(materialIds, locks, "material_id");

        return new Dictionary<string, object>
        {
            ["order_id"] = orderId,
            ["stock_locks"] = locks,
            ["material_stocks"] = QueryRowsByIds(connection, "MATERIAL_STOCK", "MATERIAL_ID", materialIds),
        };
    }

    private static Dictionary<string, object> CaptureStockLockById(OracleConnection connection, object request)
    {
        long lockId = GetLong(request, "LockId");
        Dictionary<string, object>? stockLock = QueryOne(
            connection,
            "SELECT * FROM STOCK_LOCK WHERE LOCK_ID = :id",
            ("id", lockId));
        HashSet<long> materialIds = [];
        if (stockLock is not null)
        {
            AddLongValues(materialIds, [stockLock], "material_id");
        }

        return new Dictionary<string, object>
        {
            ["stock_lock"] = stockLock!,
            ["material_stocks"] = QueryRowsByIds(connection, "MATERIAL_STOCK", "MATERIAL_ID", materialIds),
        };
    }

    private static Dictionary<string, object> CaptureProductionOrder(OracleConnection connection, object request)
    {
        long orderId = GetLong(request, "OrderId");
        return new Dictionary<string, object>
        {
            ["production_order"] = QueryOne(connection, "SELECT * FROM PRODUCTION_ORDER WHERE ORDER_ID = :id", ("id", orderId))!,
            ["stock_locks"] = QueryRows(connection, "SELECT * FROM STOCK_LOCK WHERE ORDER_ID = :id ORDER BY LOCK_ID", ("id", orderId)),
            ["finish_inbounds"] = QueryRows(connection, "SELECT * FROM FINISH_INBOUND WHERE ORDER_ID = :id ORDER BY INBOUND_ID", ("id", orderId)),
            ["material_stocks"] = QueryRows(
                connection,
                @"SELECT MS.* FROM MATERIAL_STOCK MS
                  WHERE MS.MATERIAL_ID IN (
                      SELECT MATERIAL_ID FROM PRODUCTION_ORDER WHERE ORDER_ID = :id
                      UNION
                      SELECT MATERIAL_ID FROM STOCK_LOCK WHERE ORDER_ID = :id
                  )
                  ORDER BY MS.MATERIAL_ID",
                ("id", orderId)),
        };
    }

    private static Dictionary<string, object> CaptureLineType(OracleConnection connection, object request)
    {
        object? typeId = GetValue(request, "TypeId");
        Dictionary<string, object>? row = typeId is null
            ? QueryOne(connection, "SELECT * FROM LINE_TYPE WHERE TYPE_NAME = :name", ("name", GetValue(request, "TypeName")))
            : QueryOne(connection, "SELECT * FROM LINE_TYPE WHERE TYPE_ID = :id", ("id", typeId));
        return Single("line_type", row);
    }

    private static Dictionary<string, object> CaptureProductionLine(OracleConnection connection, object request)
    {
        long lineId = GetLong(request, "LineId");
        return new Dictionary<string, object>
        {
            ["production_line"] = QueryOne(connection, "SELECT * FROM PRODUCTION_LINE WHERE LINE_ID = :id", ("id", lineId))!,
            ["line_status"] = QueryOne(connection, "SELECT * FROM LINE_STATUS WHERE LINE_ID = :id", ("id", lineId))!,
        };
    }

    private static Dictionary<string, object> CaptureCapacityConfig(OracleConnection connection, object request)
    {
        object? configId = GetValue(request, "ConfigId");
        Dictionary<string, object>? row = configId is null
            ? QueryOne(
                connection,
                "SELECT * FROM CAPACITY_CONFIG WHERE MATERIAL_ID = :materialId AND TYPE_ID = :typeId",
                ("materialId", GetLong(request, "MaterialId")),
                ("typeId", GetLong(request, "TypeId")))
            : QueryOne(connection, "SELECT * FROM CAPACITY_CONFIG WHERE CONFIG_ID = :id", ("id", configId));
        return Single("capacity_config", row);
    }

    private static Dictionary<string, object> CaptureFaultRecord(OracleConnection connection, object request)
    {
        long faultId = GetLong(request, "FaultId");
        Dictionary<string, object>? fault = QueryOne(
            connection,
            "SELECT * FROM FAULT_RECORD WHERE FAULT_ID = :id",
            ("id", faultId));
        object? lineId = fault?.GetValueOrDefault("line_id");
        return new Dictionary<string, object>
        {
            ["fault_record"] = fault!,
            ["line_status"] = lineId is null
                ? null!
                : QueryOne(connection, "SELECT * FROM LINE_STATUS WHERE LINE_ID = :id", ("id", lineId))!,
        };
    }

    private static Dictionary<string, object> CapturePurchaseOrder(OracleConnection connection, object request)
    {
        long orderId = GetLong(request, "OrderId");
        return new Dictionary<string, object>
        {
            ["purchase_order"] = QueryOne(connection, "SELECT * FROM PURCHASE_ORDER WHERE ORDER_ID = :id", ("id", orderId))!,
            ["items"] = QueryRows(connection, "SELECT * FROM PURCHASE_ORDER_ITEM WHERE ORDER_ID = :id ORDER BY ITEM_ID", ("id", orderId)),
            ["receipts"] = QueryRows(connection, "SELECT * FROM RECEIVE_RECORD WHERE ORDER_ID = :id ORDER BY RECEIVE_ID", ("id", orderId)),
            ["overdue_reminders"] = QueryRows(connection, "SELECT * FROM OVERDUE_REMINDER WHERE ORDER_ID = :id ORDER BY REMINDER_ID", ("id", orderId)),
        };
    }

    private static object GetRequest(IDictionary<string, object?> actionArguments)
    {
        if (actionArguments.TryGetValue("request", out object? request) && request is not null)
        {
            return request;
        }

        return actionArguments.Values.FirstOrDefault(value => value is not null)
            ?? throw new InvalidOperationException("操作审计无法取得请求参数");
    }

    private static object? GetValue(object source, string propertyName)
    {
        PropertyInfo property = source.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"请求类型 {source.GetType().Name} 缺少属性 {propertyName}");
        object? value = property.GetValue(source);
        return value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : value;
    }

    private static long GetLong(object source, string propertyName) =>
        Convert.ToInt64(GetValue(source, propertyName));

    private static HashSet<long> GetNestedLongs(object source, string collectionProperty, string itemProperty)
    {
        var result = new HashSet<long>();
        if (GetValue(source, collectionProperty) is not IEnumerable items)
        {
            return result;
        }

        foreach (object item in items)
        {
            result.Add(GetLong(item, itemProperty));
        }

        return result;
    }

    private static void AddLongValues(
        ISet<long> target,
        IEnumerable<Dictionary<string, object>> rows,
        string columnName)
    {
        foreach (Dictionary<string, object> row in rows)
        {
            if (row.GetValueOrDefault(columnName) is { } value)
            {
                target.Add(Convert.ToInt64(value));
            }
        }
    }

    private static Dictionary<string, object> Single(string name, object? value) =>
        new() { [name] = value! };

    private static Dictionary<string, object>? QueryOne(
        OracleConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters) =>
        QueryRows(connection, sql, parameters).FirstOrDefault();

    private static List<Dictionary<string, object>> QueryRows(
        OracleConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using OracleCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.Add(new OracleParameter(name, value ?? DBNull.Value));
        }

        var rows = new List<Dictionary<string, object>>();
        using OracleDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index).ToLowerInvariant()] = ReadValue(reader, index)!;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static List<Dictionary<string, object>> QueryRowsByIds(
        OracleConnection connection,
        string tableName,
        string columnName,
        IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var parameters = ids
            .Select((id, index) => ($"id{index}", (object?)id))
            .ToArray();
        string placeholders = string.Join(", ", parameters.Select(parameter => $":{parameter.Item1}"));
        return QueryRows(
            connection,
            $"SELECT * FROM {tableName} WHERE {columnName} IN ({placeholders}) ORDER BY {columnName}",
            parameters);
    }

    private static object? ReadValue(OracleDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return null;
        }

        object value = reader.GetValue(index);
        if (value is OracleClob clob)
        {
            string text = clob.Value;
            try
            {
                return JToken.Parse(text);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return text;
            }
        }

        return value;
    }
}
