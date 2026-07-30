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
        if (!reader.IsDBNull(6)) log.BeforeData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.GetString(6))!;
        if (!reader.IsDBNull(7)) log.AfterData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.GetString(7))!;
        return log;
    }

    public (List<OperationLog> Records, int Total) List(
        int page, int pageSize,
        string? module, string? action, int? operatorId,
        DateTime? startTime, DateTime? endTime)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var parameters = new List<OracleParameter>();

        if (!string.IsNullOrWhiteSpace(module))
        {
            conditions.Add("MODULE LIKE :module");
            parameters.Add(new OracleParameter("module", $"%{module.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            conditions.Add("ACTION LIKE :action");
            parameters.Add(new OracleParameter("action", $"%{action.Trim()}%"));
        }
        if (operatorId.HasValue)
        {
            conditions.Add("OPERATOR_ID = :operatorId");
            parameters.Add(new OracleParameter("operatorId", operatorId.Value));
        }
        if (startTime.HasValue)
        {
            conditions.Add("OPERATE_TIME >= :startTime");
            parameters.Add(new OracleParameter("startTime", startTime.Value));
        }
        if (endTime.HasValue)
        {
            conditions.Add("OPERATE_TIME <= :endTime");
            parameters.Add(new OracleParameter("endTime", endTime.Value));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM OPERATION_LOG {where}";
        foreach (var p in parameters) countCmd.Parameters.Add(p);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        var offset = (page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"{SelectColumns} {where} ORDER BY OPERATE_TIME DESC OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        foreach (var p in parameters) dataCmd.Parameters.Add(p);

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
        cmd.Parameters.Add(new OracleParameter("module", request.Module.Trim()));
        cmd.Parameters.Add(new OracleParameter("action", request.Action.Trim()));
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
        getCmd.CommandText = $"{SelectColumns} WHERE LOG_ID = :logId";
        getCmd.Parameters.Add(new OracleParameter("logId", logId));
        using var reader = getCmd.ExecuteReader();
        return reader.Read() ? ReadOperationLog(reader) : new OperationLog
        {
            LogId = logId,
            Module = request.Module,
            Action = request.Action,
            OperatorId = request.OperatorId,
            OperateTime = DateTime.UtcNow,
            IpAddress = request.IpAddress ?? null!,
        };
    }
}
