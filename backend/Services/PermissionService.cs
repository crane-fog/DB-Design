using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理中的只读权限目录。</summary>
public class PermissionService(string connString)
{
    private const string SelectColumns = @"
        SELECT PERMISSION_ID,
               PERMISSION_CODE,
               MODULE_NAME,
               RESOURCE_NAME,
               ACTION_NAME,
               DESCRIPTION,
               SORT_ORDER,
               STATUS
        FROM SYS_PERMISSION";

    public (List<PermissionBrief> Records, int Total) List(
        int page,
        int pageSize,
        int? permissionId,
        string? permissionCode,
        string? moduleName,
        string? resourceName,
        string? actionName)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();
        AddExactFilter(conditions, filters, permissionId);
        AddLikeFilter(conditions, filters, "PERMISSION_CODE", "permissionCode", permissionCode);
        AddLikeFilter(conditions, filters, "MODULE_NAME", "moduleName", moduleName);
        AddLikeFilter(conditions, filters, "RESOURCE_NAME", "resourceName", resourceName);
        AddLikeFilter(conditions, filters, "ACTION_NAME", "actionName", actionName);
        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        using var countCmd = conn.CreateCommand();
        countCmd.BindByName = true;
        countCmd.CommandText = string.Concat("SELECT COUNT(*) FROM SYS_PERMISSION ", where);
        OracleSql.AddFilters(countCmd, filters);
        int total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        long offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.BindByName = true;
        dataCmd.CommandText = string.Concat(
            SelectColumns,
            " ",
            where,
            " ORDER BY SORT_ORDER, PERMISSION_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY");
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<PermissionBrief>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadPermission(reader));
        }

        return (records, total);
    }

    public PermissionBrief? Get(int permissionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " WHERE PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("permissionId", permissionId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadPermission(reader) : null;
    }

    private static void AddExactFilter(
        ICollection<string> conditions,
        ICollection<SqlFilter> filters,
        int? permissionId)
    {
        if (!permissionId.HasValue)
        {
            return;
        }

        conditions.Add("PERMISSION_ID = :permissionId");
        filters.Add(new SqlFilter("permissionId", permissionId.Value));
    }

    private static void AddLikeFilter(
        ICollection<string> conditions,
        ICollection<SqlFilter> filters,
        string column,
        string parameter,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        conditions.Add($"{column} LIKE :{parameter}");
        filters.Add(new SqlFilter(parameter, $"%{value.Trim()}%"));
    }

    private static PermissionBrief ReadPermission(OracleDataReader reader)
    {
        string databaseCode = reader.GetString(1);
        if (!PermissionCodeMapper.TryParse(databaseCode, out PermissionCode permissionCode))
        {
            throw new InvalidOperationException($"数据库包含 API 契约未定义的权限码：{databaseCode}");
        }

        return new PermissionBrief
        {
            PermissionId = Convert.ToInt32(reader.GetValue(0)),
            PermissionCode = permissionCode,
            ModuleName = reader.GetString(2),
            ResourceName = reader.GetString(3),
            ActionName = reader.GetString(4),
            Description = reader.IsDBNull(5) ? null! : reader.GetString(5),
            SortOrder = Convert.ToInt32(reader.GetValue(6)),
            Status = string.Equals(reader.GetString(7), "valid", StringComparison.OrdinalIgnoreCase)
                ? PermissionBrief.StatusEnum.ValidEnum
                : PermissionBrief.StatusEnum.DisabledEnum,
        };
    }
}
