using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理 — 权限 CRUD Service。</summary>
public class PermissionService(string connString)
{
    private const string SelectColumns = @"
        SELECT PERMISSION_ID, ""resource"", ACTION
        FROM SYS_PERMISSION";

    private static PermissionBrief ReadPermission(OracleDataReader reader)
    {
        return new PermissionBrief
        {
            PermissionId = Convert.ToInt32(reader.GetValue(0)),
            Resource = reader.GetString(1),
            Action = reader.GetString(2),
        };
    }

    /// <summary>查询已从登录态解析的用户的有效权限，不分页，不重复返回共享权限。</summary>
    public List<Permission> GetEffectivePermissions(CurrentUser user)
    {
        if (user.RoleNames.Count == 0) return [];

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns;

        if (!user.RoleNames.Contains(AuthorizationService.AdminRole))
        {
            cmd.CommandText += @" WHERE EXISTS (
                SELECT 1
                FROM SYS_USER_ROLE UR
                JOIN SYS_ROLE R ON R.ROLE_ID = UR.ROLE_ID
                JOIN SYS_ROLE_PERMISSION RP ON RP.ROLE_ID = R.ROLE_ID
                WHERE UR.USER_ID = :userId
                  AND R.STATUS = 'valid'
                  AND RP.PERMISSION_ID = SYS_PERMISSION.PERMISSION_ID)";
            cmd.Parameters.Add(new OracleParameter("userId", user.UserId));
        }

        cmd.CommandText += " ORDER BY PERMISSION_ID";
        var permissions = new List<Permission>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            permissions.Add(new Permission
            {
                PermissionId = Convert.ToInt32(reader.GetValue(0)),
                Resource = reader.GetString(1),
                Action = reader.GetString(2),
            });
        }

        return permissions;
    }

    public (List<PermissionBrief> Records, int Total) List(
        int page, int pageSize, int? permissionId, string? resource, string? action)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (permissionId.HasValue)
        {
            conditions.Add("PERMISSION_ID = :permissionId");
            filters.Add(new SqlFilter("permissionId", permissionId.Value));
        }
        if (!string.IsNullOrWhiteSpace(resource))
        {
            conditions.Add("\"resource\" LIKE :res");
            filters.Add(new SqlFilter("res", $"%{resource.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            conditions.Add("ACTION LIKE :action");
            filters.Add(new SqlFilter("action", $"%{action.Trim()}%"));
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = string.Concat("SELECT COUNT(*) FROM SYS_PERMISSION ", where);
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = string.Concat(SelectColumns, " ", where, " ORDER BY PERMISSION_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY");
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<PermissionBrief>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read()) records.Add(ReadPermission(reader));

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

    public (PermissionBrief? Permission, string? ErrorMessage) Create(PermissionCreateRequest request)
    {
        // 契约未声明 maxLength，超长输入会触发 ORA-12899（值过大）；
        // 服务层统一拦截（列定义见 database/01_schema_forklift.sql）。
        // 注意：直接传模型属性（CheckMaxLength 内部判空），不要对属性使用 ?.Trim()，
        // 否则编译器会将属性标记为可能为 null，导致后续解引用触发 CS8602。
        var lengthError = InputGuard.CheckMaxLength(request.Resource, 100, "资源")
            ?? InputGuard.CheckMaxLength(request.Action, 50, "操作");
        if (lengthError is not null)
        {
            return (null, lengthError);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SYS_PERMISSION (""resource"", ACTION)
                            VALUES (:res, :action)
                            RETURNING PERMISSION_ID INTO :permissionId";
        cmd.Parameters.Add(new OracleParameter("res", request.Resource.Trim()));
        cmd.Parameters.Add(new OracleParameter("action", request.Action.Trim()));
        var permissionIdOut = new OracleParameter("permissionId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(permissionIdOut);

        try
        {
            cmd.ExecuteNonQuery();
            var id = Convert.ToInt32(permissionIdOut.Value.ToString());
            return (Get(id), null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "权限已存在（资源+操作重复）");
        }
    }

    public (PermissionBrief? Permission, string? ErrorMessage) Update(PermissionUpdateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var existing = Get(request.PermissionId);
        if (existing is null) return (null, "权限不存在");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE SYS_PERMISSION
                            SET ""resource"" = :res, ACTION = :action
                            WHERE PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("res", request.Resource.Trim()));
        cmd.Parameters.Add(new OracleParameter("action", request.Action.Trim()));
        cmd.Parameters.Add(new OracleParameter("permissionId", request.PermissionId));

        try
        {
            cmd.ExecuteNonQuery();
            return (Get(request.PermissionId), null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "权限已存在（资源+操作重复）");
        }
    }

    public (bool Ok, string? ErrorMessage) Delete(int permissionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_PERMISSION WHERE PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("permissionId", permissionId));

        try
        {
            return (cmd.ExecuteNonQuery() > 0, null);
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            // ORA-02292：子记录存在（角色权限），转换为业务错误
            return (false, "该权限仍被角色关联，无法删除；请先移除关联");
        }
    }
}
