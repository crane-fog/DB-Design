using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理 — 用户角色分配 Service。</summary>
public class UserRoleService(string connString)
{
    public (List<UserRole> Records, int Total) List(int page, int pageSize, int? userId, int? roleId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (userId.HasValue)
        {
            conditions.Add("UR.USER_ID = :userId");
            filters.Add(new SqlFilter("userId", userId.Value));
        }
        if (roleId.HasValue)
        {
            conditions.Add("UR.ROLE_ID = :roleId");
            filters.Add(new SqlFilter("roleId", roleId.Value));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_USER_ROLE UR {where}";
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        var offset = (page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"SELECT UR.USER_ID, UR.ROLE_ID
                                 FROM SYS_USER_ROLE UR {where}
                                 ORDER BY UR.USER_ID, UR.ROLE_ID
                                 OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<UserRole>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new UserRole
            {
                UserId = Convert.ToInt32(reader.GetValue(0)),
                RoleId = Convert.ToInt32(reader.GetValue(1)),
            });
        }

        return (records, total);
    }

    public (List<UserRole>? UserRoles, string? ErrorMessage) Assign(int userId, List<int> roleIds)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        // 检查用户存在
        using var userCheck = conn.CreateCommand();
        userCheck.CommandText = "SELECT COUNT(*) FROM SYS_USER WHERE USER_ID = :userId";
        userCheck.Parameters.Add(new OracleParameter("userId", userId));
        if (Convert.ToInt32(userCheck.ExecuteScalar()!) == 0)
        {
            return (null, "用户不存在");
        }

        var assigned = new List<UserRole>();

        foreach (var roleId in roleIds)
        {
            // 检查角色存在
            using var roleCheck = conn.CreateCommand();
            roleCheck.CommandText = "SELECT COUNT(*) FROM SYS_ROLE WHERE ROLE_ID = :roleId";
            roleCheck.Parameters.Add(new OracleParameter("roleId", roleId));
            if (Convert.ToInt32(roleCheck.ExecuteScalar()!) == 0)
            {
                return (null, $"角色 {roleId} 不存在");
            }

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"INSERT INTO SYS_USER_ROLE (USER_ID, ROLE_ID)
                                      VALUES (:userId, :roleId)";
            insertCmd.Parameters.Add(new OracleParameter("userId", userId));
            insertCmd.Parameters.Add(new OracleParameter("roleId", roleId));

            try
            {
                insertCmd.ExecuteNonQuery();
                assigned.Add(new UserRole { UserId = userId, RoleId = roleId });
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                // 已存在的关联，跳过
            }
        }

        return (assigned, null);
    }

    public bool Delete(int userId, int roleId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_USER_ROLE WHERE USER_ID = :userId AND ROLE_ID = :roleId";
        cmd.Parameters.Add(new OracleParameter("userId", userId));
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));

        return cmd.ExecuteNonQuery() > 0;
    }
}
