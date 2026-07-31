using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>登录日志查询 Service。</summary>
public class LoginLogService(string connString)
{
    private const string SelectColumns = @"
        SELECT LOG_ID, USER_ID, LOGIN_TIME, IP_ADDRESS, RESULT, FAIL_REASON
        FROM LOGIN_LOG";

    private static LoginLog ReadLoginLog(OracleDataReader reader)
    {
        var log = new LoginLog
        {
            LogId = Convert.ToInt32(reader.GetValue(0)),
            // 工号不存在等失败登录场景会写入 NULL user_id，读取时必须判空，
            // 否则 Convert.ToInt32(DBNull.Value) 抛异常导致整个日志列表查询失败。
            // 当前生成的 LoginLog.UserId 为非空 int，用 0 表示“未知用户”；
            // 待契约 nullable:true 重新生成后应改为 null。
            UserId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
            LoginTime = reader.GetDateTime(2),
            IpAddress = reader.GetString(3),
            Result = reader.GetString(4) == "成功" ? LoginLog.ResultEnum.SuccessEnum : LoginLog.ResultEnum.FailureEnum,
        };
        if (!reader.IsDBNull(5)) log.FailReason = reader.GetString(5);
        return log;
    }

    public (List<LoginLog> Records, int Total) List(
        int page, int pageSize, int? userId, string? result, DateTime? startTime, DateTime? endTime)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (userId.HasValue)
        {
            conditions.Add("USER_ID = :userId");
            filters.Add(new SqlFilter("userId", userId.Value));
        }
        if (!string.IsNullOrWhiteSpace(result))
        {
            var dbResult = result.Trim() == "success" ? "成功" : "失败";
            conditions.Add("RESULT = :result");
            filters.Add(new SqlFilter("result", dbResult));
        }
        if (startTime.HasValue)
        {
            conditions.Add("LOGIN_TIME >= :startTime");
            filters.Add(new SqlFilter("startTime", startTime.Value));
        }
        if (endTime.HasValue)
        {
            conditions.Add("LOGIN_TIME <= :endTime");
            filters.Add(new SqlFilter("endTime", endTime.Value));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM LOGIN_LOG {where}";
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"{SelectColumns} {where} ORDER BY LOGIN_TIME DESC OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<LoginLog>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read()) records.Add(ReadLoginLog(reader));

        return (records, total);
    }

    /// <summary>写入登录日志（供 AuthService 调用）。userId 为 0 或负数时写入 NULL（工号不存在场景）。</summary>
    public void Write(long userId, string ipAddress, bool success, string? failReason)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO LOGIN_LOG (USER_ID, LOGIN_TIME, IP_ADDRESS, RESULT, FAIL_REASON)
                            VALUES (:userId, SYSTIMESTAMP, :ipAddress, :result, :failReason)";
        cmd.Parameters.Add(new OracleParameter("userId", userId > 0 ? (object)userId : DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("ipAddress", ipAddress));
        cmd.Parameters.Add(new OracleParameter("result", success ? "成功" : "失败"));
        cmd.Parameters.Add(new OracleParameter("failReason",
            string.IsNullOrWhiteSpace(failReason) ? (object)DBNull.Value : failReason));

        cmd.ExecuteNonQuery();
    }
}
