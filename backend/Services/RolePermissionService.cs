using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理 — 角色权限分配 Service。</summary>
public class RolePermissionService(string connString)
{
    public (List<RolePermission> Records, int Total) List(int page, int pageSize, int? roleId, int? permissionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (roleId.HasValue)
        {
            conditions.Add("RP.ROLE_ID = :roleId");
            filters.Add(new SqlFilter("roleId", roleId.Value));
        }
        if (permissionId.HasValue)
        {
            conditions.Add("RP.PERMISSION_ID = :permissionId");
            filters.Add(new SqlFilter("permissionId", permissionId.Value));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_ROLE_PERMISSION RP {where}";
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"SELECT RP.ROLE_ID, RP.PERMISSION_ID
                                 FROM SYS_ROLE_PERMISSION RP {where}
                                 ORDER BY RP.ROLE_ID, RP.PERMISSION_ID
                                 OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<RolePermission>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new RolePermission
            {
                RoleId = Convert.ToInt32(reader.GetValue(0)),
                PermissionId = Convert.ToInt32(reader.GetValue(1)),
            });
        }

        return (records, total);
    }

    public (List<RolePermission>? RolePermissions, string? ErrorMessage) Assign(int roleId, List<int> permissionIds)
    {
        var distinctIds = permissionIds.Distinct().ToList();

        using var conn = new OracleConnection(connString);
        conn.Open();

        // 1. 检查角色存在且有效：disabled 角色不应再授予新权限（与 disabled 角色不参与授权一致）
        string? roleStatus;
        using (var roleCheck = conn.CreateCommand())
        {
            roleCheck.CommandText = "SELECT STATUS FROM SYS_ROLE WHERE ROLE_ID = :roleId";
            roleCheck.Parameters.Add(new OracleParameter("roleId", roleId));
            var value = roleCheck.ExecuteScalar();
            if (value is null || value is DBNull)
            {
                return (null, "角色不存在");
            }

            roleStatus = Convert.ToString(value);
        }

        if (!string.Equals(roleStatus, "valid", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "角色已停用，无法分配权限");
        }

        // 2. 写入前一次性校验所有权限存在；任一项不通过则整体失败，不做任何写入
        if (distinctIds.Count > 0)
        {
            var existing = LoadExistingPermissionIds(conn, distinctIds);
            foreach (var permissionId in distinctIds)
            {
                if (!existing.Contains(permissionId))
                {
                    return (null, $"权限 {permissionId} 不存在");
                }
            }
        }

        // 3. 事务内批量插入；任一项失败回滚全部修改
        using var tx = conn.BeginTransaction();
        var assigned = new List<RolePermission>();
        try
        {
            foreach (var permissionId in distinctIds)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"INSERT INTO SYS_ROLE_PERMISSION (ROLE_ID, PERMISSION_ID)
                                          VALUES (:roleId, :permissionId)";
                insertCmd.Parameters.Add(new OracleParameter("roleId", roleId));
                insertCmd.Parameters.Add(new OracleParameter("permissionId", permissionId));

                try
                {
                    insertCmd.ExecuteNonQuery();
                    assigned.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
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

    /// <summary>一次查询所有目标权限 ID，避免逐项查询的 N+1 模式。</summary>
    private static HashSet<int> LoadExistingPermissionIds(OracleConnection conn, List<int> permissionIds)
    {
        var result = new HashSet<int>();
        if (permissionIds.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        var binds = new List<string>();
        for (var i = 0; i < permissionIds.Count; i++)
        {
            var name = $":pid{i}";
            binds.Add(name);
            cmd.Parameters.Add(new OracleParameter(name, permissionIds[i]));
        }

        // 注意：Oracle IN 列表上限为 1000 项，权限分配场景远小于该限制。
        cmd.CommandText = $"SELECT PERMISSION_ID FROM SYS_PERMISSION WHERE PERMISSION_ID IN ({string.Join(", ", binds)})";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Convert.ToInt32(reader.GetValue(0)));
        }

        return result;
    }

    public bool Delete(int roleId, int permissionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_ROLE_PERMISSION WHERE ROLE_ID = :roleId AND PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));
        cmd.Parameters.Add(new OracleParameter("permissionId", permissionId));

        return cmd.ExecuteNonQuery() > 0;
    }
}
