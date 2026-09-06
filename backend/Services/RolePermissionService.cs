using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理中的角色权限关系查询与集合替换。</summary>
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

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
        using var countCmd = conn.CreateCommand();
        countCmd.BindByName = true;
        countCmd.CommandText = string.Concat("SELECT COUNT(*) FROM SYS_ROLE_PERMISSION RP ", where);
        OracleSql.AddFilters(countCmd, filters);
        int total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        long offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.BindByName = true;
        dataCmd.CommandText = string.Concat(
            "SELECT RP.ROLE_ID, RP.PERMISSION_ID FROM SYS_ROLE_PERMISSION RP ",
            where,
            " ORDER BY RP.ROLE_ID, RP.PERMISSION_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY");
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

    public (List<RolePermission>? RolePermissions, string? ErrorMessage) Set(
        int roleId,
        List<int> permissionIds)
    {
        List<int> desiredIds = permissionIds.Distinct().OrderBy(id => id).ToList();
        if (desiredIds.Count > 1000)
        {
            return (null, "单次设置的权限数量不能超过 1000");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using OracleTransaction transaction = conn.BeginTransaction();
        try
        {
            string? roleStatus = LoadRoleStatus(conn, transaction, roleId);
            if (roleStatus is null)
            {
                transaction.Rollback();
                return (null, "角色不存在");
            }

            if (desiredIds.Count > 0
                && !string.Equals(roleStatus, "valid", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return (null, "角色已停用，无法分配权限");
            }

            Dictionary<int, string> statuses = LoadPermissionStatuses(conn, transaction, desiredIds);
            foreach (int permissionId in desiredIds)
            {
                if (!statuses.TryGetValue(permissionId, out string? status))
                {
                    transaction.Rollback();
                    return (null, $"权限 {permissionId} 不存在");
                }

                if (!string.Equals(status, "valid", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    return (null, $"权限 {permissionId} 已停用，无法分配");
                }
            }

            HashSet<int> currentIds = LoadAssignedPermissionIds(conn, transaction, roleId);
            List<int> idsToDelete = currentIds.Except(desiredIds).ToList();
            List<int> idsToInsert = desiredIds.Except(currentIds).ToList();
            DeleteAssignments(conn, transaction, roleId, idsToDelete);
            InsertAssignments(conn, transaction, roleId, idsToInsert);
            transaction.Commit();

            return (desiredIds.Select(permissionId => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
            }).ToList(), null);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string? LoadRoleStatus(OracleConnection conn, OracleTransaction transaction, int roleId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT STATUS FROM SYS_ROLE WHERE ROLE_ID = :roleId";
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));
        object? value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static Dictionary<int, string> LoadPermissionStatuses(
        OracleConnection conn,
        OracleTransaction transaction,
        IReadOnlyList<int> permissionIds)
    {
        var result = new Dictionary<int, string>();
        if (permissionIds.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.BindByName = true;
        var binds = new List<string>();
        for (int index = 0; index < permissionIds.Count; index++)
        {
            string name = $"permissionId{index}";
            binds.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, permissionIds[index]));
        }

        cmd.CommandText = $"SELECT PERMISSION_ID, STATUS FROM SYS_PERMISSION WHERE PERMISSION_ID IN ({string.Join(", ", binds)})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[Convert.ToInt32(reader.GetValue(0))] = reader.GetString(1);
        }

        return result;
    }

    private static HashSet<int> LoadAssignedPermissionIds(
        OracleConnection conn,
        OracleTransaction transaction,
        int roleId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT PERMISSION_ID FROM SYS_ROLE_PERMISSION WHERE ROLE_ID = :roleId FOR UPDATE";
        cmd.Parameters.Add(new OracleParameter("roleId", roleId));
        var result = new HashSet<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Convert.ToInt32(reader.GetValue(0)));
        }

        return result;
    }

    private static void DeleteAssignments(
        OracleConnection conn,
        OracleTransaction transaction,
        int roleId,
        IReadOnlyList<int> permissionIds)
    {
        if (permissionIds.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.ArrayBindCount = permissionIds.Count;
        cmd.CommandText = "DELETE FROM SYS_ROLE_PERMISSION WHERE ROLE_ID = :roleId AND PERMISSION_ID = :permissionId";
        cmd.Parameters.Add("roleId", OracleDbType.Int32).Value = Enumerable.Repeat(roleId, permissionIds.Count).ToArray();
        cmd.Parameters.Add("permissionId", OracleDbType.Int32).Value = permissionIds.ToArray();
        cmd.ExecuteNonQuery();
    }

    private static void InsertAssignments(
        OracleConnection conn,
        OracleTransaction transaction,
        int roleId,
        IReadOnlyList<int> permissionIds)
    {
        if (permissionIds.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.ArrayBindCount = permissionIds.Count;
        cmd.CommandText = "INSERT INTO SYS_ROLE_PERMISSION (ROLE_ID, PERMISSION_ID) VALUES (:roleId, :permissionId)";
        cmd.Parameters.Add("roleId", OracleDbType.Int32).Value = Enumerable.Repeat(roleId, permissionIds.Count).ToArray();
        cmd.Parameters.Add("permissionId", OracleDbType.Int32).Value = permissionIds.ToArray();
        cmd.ExecuteNonQuery();
    }
}
