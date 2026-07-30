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
        var parameters = new List<OracleParameter>();

        if (roleId.HasValue)
        {
            conditions.Add("ROLE_ID = :roleId");
            parameters.Add(new OracleParameter("roleId", roleId.Value));
        }
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            conditions.Add("ROLE_NAME LIKE :roleName");
            parameters.Add(new OracleParameter("roleName", $"%{roleName.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("STATUS = :status");
            parameters.Add(new OracleParameter("status", status.Trim()));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_ROLE {where}";
        foreach (var p in parameters) countCmd.Parameters.Add(p);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        var offset = (page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"{SelectColumns} {where} ORDER BY ROLE_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        foreach (var p in parameters) dataCmd.Parameters.Add(p);

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

        sets.Add("DESCRIPTION = :description");
        parameters.Add(new OracleParameter("description",
            string.IsNullOrWhiteSpace(request.Description) ? (object)DBNull.Value : request.Description.Trim()));

        var statusVal = request.Status == RoleUpdateRequest.StatusEnum.DisabledEnum ? "disabled" : "valid";
        sets.Add("STATUS = :status");
        parameters.Add(new OracleParameter("status", statusVal));

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

    public bool Delete(int roleId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_ROLE WHERE ROLE_ID = :roleId";
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));

        return cmd.ExecuteNonQuery() > 0;
    }
}
