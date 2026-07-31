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

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
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
        var distinctIds = roleIds.Distinct().ToList();

        using var conn = new OracleConnection(connString);
        conn.Open();

        // 1. 检查用户存在且有效：disabled 用户不应再分配新角色（与 disabled 角色不参与授权对称）
        string? userStatus;
        using (var userCheck = conn.CreateCommand())
        {
            userCheck.CommandText = "SELECT STATUS FROM SYS_USER WHERE USER_ID = :userId";
            userCheck.Parameters.Add(new OracleParameter("userId", userId));
            var value = userCheck.ExecuteScalar();
            if (value is null || value is DBNull)
            {
                return (null, "用户不存在");
            }

            userStatus = Convert.ToString(value);
        }

        if (!string.Equals(userStatus, "valid", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "用户已停用，无法分配角色");
        }

        // 2. 写入前一次性校验所有角色：必须存在且为有效状态。
        //    任一项不通过则整体失败，不做任何写入，保证批量分配原子性。
        if (distinctIds.Count > 0)
        {
            var statuses = LoadRoleStatuses(conn, distinctIds);
            foreach (var roleId in distinctIds)
            {
                if (!statuses.TryGetValue(roleId, out var status))
                {
                    return (null, $"角色 {roleId} 不存在");
                }

                if (status != "valid")
                {
                    return (null, $"角色 {roleId} 已停用，无法分配");
                }
            }
        }

        // 3. 事务内批量插入；任一项失败回滚全部修改
        using var tx = conn.BeginTransaction();
        var assigned = new List<UserRole>();
        try
        {
            foreach (var roleId in distinctIds)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
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

            tx.Commit();
            return (assigned, null);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>一次查询所有目标角色的状态（角色 ID → status），避免逐项查询的 N+1 模式。</summary>
    private static Dictionary<int, string> LoadRoleStatuses(OracleConnection conn, List<int> roleIds)
    {
        var result = new Dictionary<int, string>();
        if (roleIds.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        var binds = new List<string>();
        for (var i = 0; i < roleIds.Count; i++)
        {
            var name = $":rid{i}";
            binds.Add(name);
            cmd.Parameters.Add(new OracleParameter(name, roleIds[i]));
        }

        // 注意：Oracle IN 列表上限为 1000 项，角色分配场景远小于该限制。
        cmd.CommandText = $"SELECT ROLE_ID, STATUS FROM SYS_ROLE WHERE ROLE_ID IN ({string.Join(", ", binds)})";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[Convert.ToInt32(reader.GetValue(0))] = reader.GetString(1);
        }

        return result;
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
