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
        var parameters = new List<OracleParameter>();

        if (roleId.HasValue)
        {
            conditions.Add("RP.ROLE_ID = :roleId");
            parameters.Add(new OracleParameter("roleId", roleId.Value));
        }
        if (permissionId.HasValue)
        {
            conditions.Add("RP.PERMISSION_ID = :permissionId");
            parameters.Add(new OracleParameter("permissionId", permissionId.Value));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_ROLE_PERMISSION RP {where}";
        foreach (var p in parameters) countCmd.Parameters.Add(p);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        var offset = (page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"SELECT RP.ROLE_ID, RP.PERMISSION_ID
                                 FROM SYS_ROLE_PERMISSION RP {where}
                                 ORDER BY RP.ROLE_ID, RP.PERMISSION_ID
                                 OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        foreach (var p in parameters) dataCmd.Parameters.Add(p);

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
        using var conn = new OracleConnection(connString);
        conn.Open();

        // 检查角色存在
        using var roleCheck = conn.CreateCommand();
        roleCheck.CommandText = "SELECT COUNT(*) FROM SYS_ROLE WHERE ROLE_ID = :roleId";
        roleCheck.Parameters.Add(new OracleParameter("roleId", roleId));
        if (Convert.ToInt32(roleCheck.ExecuteScalar()!) == 0)
        {
            return (null, "角色不存在");
        }

        var assigned = new List<RolePermission>();

        foreach (var permissionId in permissionIds)
        {
            // 检查权限存在
            using var permCheck = conn.CreateCommand();
            permCheck.CommandText = "SELECT COUNT(*) FROM SYS_PERMISSION WHERE PERMISSION_ID = :permissionId";
            permCheck.Parameters.Add(new OracleParameter("permissionId", permissionId));
            if (Convert.ToInt32(permCheck.ExecuteScalar()!) == 0)
            {
                return (null, $"权限 {permissionId} 不存在");
            }

            using var insertCmd = conn.CreateCommand();
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

        return (assigned, null);
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
