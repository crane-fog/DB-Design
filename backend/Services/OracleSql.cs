using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

/// <summary>
/// 筛选条件参数占位（参数名 + 值）。
/// 只保存名称与值，绑定到命令时才创建新的 OracleParameter 实例，
/// 避免同一个 OracleParameter 实例被加入多个 ParameterCollection 触发 ORA-50030。
/// </summary>
internal sealed record SqlFilter(string Name, object Value);

/// <summary>
/// Oracle 命令绑定辅助方法。
/// </summary>
internal static class OracleSql
{
    /// <summary>
    /// 为命令绑定一组筛选参数。每次调用都会创建全新的 OracleParameter 实例，
    /// 因此同一个 SqlFilter 集合可以安全地复用于统计命令与数据查询命令。
    /// </summary>
    public static void AddFilters(OracleCommand cmd, IEnumerable<SqlFilter> filters)
    {
        foreach (var filter in filters)
        {
            cmd.Parameters.Add(new OracleParameter(filter.Name, filter.Value));
        }
    }
}
