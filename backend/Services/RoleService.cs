using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理 — 角色 CRUD Service。</summary>
public class RoleService(string connString)
{
    private const string SelectColumns = @"
        SELECT ROLE_ID, ROLE_NAME, DESCRIPTION, STATUS
        FROM SYS_ROLE";

    private static Role ReadRole(OracleDataReader reader)
    {
        var role = new Role
        {
            RoleId = Convert.ToInt32(reader.GetValue(0)),
            RoleName = reader.GetString(1),
            Status = Role.StatusEnum.ValidEnum,
        };
        if (!reader.IsDBNull(2)) role.Description = reader.GetString(2);
        var status = reader.GetString(3);
        role.Status = status == "disabled" ? Role.StatusEnum.DisabledEnum : Role.StatusEnum.ValidEnum;
        return role;
    }

    public (List<Role> Records, int Total) List(
        int page, int pageSize, int? roleId, string? roleName, string? status)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (roleId.HasValue)
        {
            conditions.Add("ROLE_ID = :roleId");
            filters.Add(new SqlFilter("roleId", roleId.Value));
        }
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            conditions.Add("ROLE_NAME LIKE :roleName");
            filters.Add(new SqlFilter("roleName", $"%{roleName.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("STATUS = :status");
            filters.Add(new SqlFilter("status", status.Trim()));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_ROLE {where}";
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"{SelectColumns} {where} ORDER BY ROLE_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<Role>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read()) records.Add(ReadRole(reader));

        return (records, total);
    }

    public Role? Get(int roleId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} WHERE ROLE_ID = :roleId";
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadRole(reader) : null;
    }

    public (Role? Role, string? ErrorMessage) Create(RoleCreateRequest request)
    {
        // 契约未声明 maxLength，超长输入会触发 ORA-12899（值过大）；
        // 服务层统一拦截（列定义见 database/01_schema_forklift.sql）。
        // 注意：直接传模型属性（CheckMaxLength 内部判空），不要对属性使用 ?.Trim()，
        // 否则编译器会将属性标记为可能为 null，导致后续解引用触发 CS8602。
        var lengthError = InputGuard.CheckMaxLength(request.RoleName, 50, "角色名称")
            ?? InputGuard.CheckMaxLength(request.Description, 200, "角色描述");
        if (lengthError is not null)
        {
            return (null, lengthError);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SYS_ROLE (ROLE_NAME, DESCRIPTION, STATUS)
                            VALUES (:roleName, :description, :status)
                            RETURNING ROLE_ID INTO :roleId";
        cmd.Parameters.Add(new OracleParameter("roleName", request.RoleName.Trim()));
        cmd.Parameters.Add(new OracleParameter("description",
            string.IsNullOrWhiteSpace(request.Description) ? (object)DBNull.Value : request.Description.Trim()));
        var status = request.Status == RoleCreateRequest.StatusEnum.DisabledEnum ? "disabled" : "valid";
        cmd.Parameters.Add(new OracleParameter("status", status));
        var roleIdOut = new OracleParameter("roleId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(roleIdOut);

        try
        {
            cmd.ExecuteNonQuery();
            var roleId = Convert.ToInt32(roleIdOut.Value.ToString());
            return (Get(roleId), null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "角色名称已存在");
        }
    }

    public (Role? Role, string? ErrorMessage) Update(RoleUpdateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var existing = Get(request.RoleId);
        if (existing is null) return (null, "角色不存在");

        var sets = new List<string>();
        var parameters = new List<OracleParameter> { new("roleId", request.RoleId) };

        sets.Add("ROLE_NAME = :roleName");
        parameters.Add(new OracleParameter("roleName", request.RoleName.Trim()));

        // 与 UserService.Update 的 email 处理对称：省略 description 时保持原值，
        // 避免“只改名称/状态却清空已有描述”；显式传空串才清空。
        if (request.Description is not null)
        {
            sets.Add("DESCRIPTION = :description");
            parameters.Add(new OracleParameter("description",
                string.IsNullOrWhiteSpace(request.Description) ? (object)DBNull.Value : request.Description.Trim()));
        }

        // Status 守卫与 UserService.Update 对称：当前生成的 RoleUpdateRequest.Status
        // 因契约 default:valid 默认 ValidEnum(1)，此条件恒真、行为不变；
        // 契约去除 default 并重新生成后默认 0，省略 status 时保持原状态，
        // 避免“只改名称/描述却把 disabled 角色意外激活”的安全问题。
        if ((int)request.Status == 1 || (int)request.Status == 2)
        {
            var statusVal = request.Status == RoleUpdateRequest.StatusEnum.DisabledEnum ? "disabled" : "valid";
            sets.Add("STATUS = :status");
            parameters.Add(new OracleParameter("status", statusVal));
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE SYS_ROLE SET {string.Join(", ", sets)} WHERE ROLE_ID = :roleId";
        foreach (var p in parameters) cmd.Parameters.Add(p);

        try
        {
            cmd.ExecuteNonQuery();
            return (Get(request.RoleId), null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "角色名称已存在");
        }
    }

    public (bool Ok, string? ErrorMessage) Delete(int roleId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_ROLE WHERE ROLE_ID = :roleId";
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));

        try
        {
            return (cmd.ExecuteNonQuery() > 0, null);
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            // ORA-02292：子记录存在（用户角色、角色权限），转换为业务错误
            return (false, "该角色仍被用户或权限关联，无法删除；请先移除关联");
        }
    }
}
