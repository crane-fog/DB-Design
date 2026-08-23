using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>
/// 操作日志 Service。
/// 作为公共审计 Service 供其他模块注入调用，记录关键业务操作。
/// </summary>
public class OperationLogService(string connString)
{
    private const string SelectColumns = @"
        SELECT LOG_ID, MODULE, ACTION, OPERATOR_ID, OPERATE_TIME, IP_ADDRESS,
               BEFORE_DATA, AFTER_DATA
        FROM OPERATION_LOG";

    private static OperationLog ReadOperationLog(OracleDataReader reader)
    {
        var log = new OperationLog
        {
            LogId = Convert.ToInt32(reader.GetValue(0)),
            Module = reader.GetString(1),
            Action = reader.GetString(2),
            OperatorId = Convert.ToInt32(reader.GetValue(3)),
            OperateTime = reader.GetDateTime(4),
            IpAddress = reader.IsDBNull(5) ? null! : reader.GetString(5),
        };
        if (!reader.IsDBNull(6)) log.BeforeData = ParseJsonObject(reader.GetString(6));
        if (!reader.IsDBNull(7)) log.AfterData = ParseJsonObject(reader.GetString(7));
        return log;
    }

    /// <summary>
    /// 安全解析 before/after 数据。
    /// 表上仅有 IS JSON 约束（允许数组/标量），只有对象类型才能映射为字典；
    /// 非对象或非法 JSON 一律跳过，避免一条异常数据导致整个日志列表查询失败。
    /// </summary>
    private static Dictionary<string, object>? ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var token = Newtonsoft.Json.Linq.JToken.Parse(json);
            return token.Type == Newtonsoft.Json.Linq.JTokenType.Object
                ? token.ToObject<Dictionary<string, object>>()
                : null;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    public (List<OperationLog> Records, int Total) List(
        int page, int pageSize,
        string? module, string? action, int? operatorId,
        DateTime? startTime, DateTime? endTime)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (!string.IsNullOrWhiteSpace(module))
        {
            conditions.Add("MODULE LIKE :module");
            filters.Add(new SqlFilter("module", $"%{module.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            conditions.Add("ACTION LIKE :action");
            filters.Add(new SqlFilter("action", $"%{action.Trim()}%"));
        }
        if (operatorId.HasValue)
        {
            conditions.Add("OPERATOR_ID = :operatorId");
            filters.Add(new SqlFilter("operatorId", operatorId.Value));
        }
        if (startTime.HasValue)
        {
            conditions.Add("OPERATE_TIME >= :startTime");
            filters.Add(new SqlFilter("startTime", startTime.Value));
        }
        if (endTime.HasValue)
        {
            conditions.Add("OPERATE_TIME <= :endTime");
            filters.Add(new SqlFilter("endTime", endTime.Value));
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = string.Concat("SELECT COUNT(*) FROM OPERATION_LOG ", where);
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = string.Concat(SelectColumns, " ", where, " ORDER BY OPERATE_TIME DESC OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY");
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<OperationLog>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read()) records.Add(ReadOperationLog(reader));

        return (records, total);
    }

    /// <summary>写入操作日志（公共审计接口，供所有模块调用）。</summary>
    public OperationLog Write(OperationLogCreateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO OPERATION_LOG (MODULE, ACTION, OPERATOR_ID, OPERATE_TIME, IP_ADDRESS, BEFORE_DATA, AFTER_DATA)
                            VALUES (:module, :action, :operatorId, SYSTIMESTAMP, :ipAddress, :beforeData, :afterData)
                            RETURNING LOG_ID INTO :logId";
        // MODULE / ACTION 为 VARCHAR2(50)，超长会触发 ORA-12899；写入前截断防御
        var module = Truncate(request.Module.Trim(), 50);
        var action = Truncate(request.Action.Trim(), 50);
        cmd.Parameters.Add(new OracleParameter("module", module));
        cmd.Parameters.Add(new OracleParameter("action", action));
        cmd.Parameters.Add(new OracleParameter("operatorId", request.OperatorId));
        cmd.Parameters.Add(new OracleParameter("ipAddress", string.IsNullOrWhiteSpace(request.IpAddress) ? (object)DBNull.Value : request.IpAddress.Trim()));
        cmd.Parameters.Add(new OracleParameter("beforeData", request.BeforeData is null ? (object)DBNull.Value : Newtonsoft.Json.JsonConvert.SerializeObject(request.BeforeData)));
        cmd.Parameters.Add(new OracleParameter("afterData", request.AfterData is null ? (object)DBNull.Value : Newtonsoft.Json.JsonConvert.SerializeObject(request.AfterData)));
        var logIdOut = new OracleParameter("logId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(logIdOut);

        cmd.ExecuteNonQuery();
        var logId = Convert.ToInt32(logIdOut.Value.ToString());

        // 回查返回完整记录
        using var getCmd = conn.CreateCommand();
        getCmd.CommandText = SelectColumns + " WHERE LOG_ID = :logId";
        getCmd.Parameters.Add(new OracleParameter("logId", logId));
        using var reader = getCmd.ExecuteReader();
        return reader.Read() ? ReadOperationLog(reader) : new OperationLog
        {
            LogId = logId,
            Module = module,
            Action = action,
            OperatorId = request.OperatorId,
            OperateTime = DateTime.UtcNow,
            IpAddress = request.IpAddress ?? null!,
        };
    }

    /// <summary>按字节/字符长度截断字符串，防止超长字段触发 ORA-12899。</summary>
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
