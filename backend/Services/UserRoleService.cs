using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理中的用户角色关系查询与集合替换。</summary>
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

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
        using var countCmd = conn.CreateCommand();
        countCmd.BindByName = true;
        countCmd.CommandText = string.Concat("SELECT COUNT(*) FROM SYS_USER_ROLE UR ", where);
        OracleSql.AddFilters(countCmd, filters);
        int total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        long offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.BindByName = true;
        dataCmd.CommandText = string.Concat(
            "SELECT UR.USER_ID, UR.ROLE_ID FROM SYS_USER_ROLE UR ",
            where,
            " ORDER BY UR.USER_ID, UR.ROLE_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY");
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

    public (List<UserRole>? UserRoles, string? ErrorMessage) Set(int userId, List<int> roleIds)
    {
        List<int> desiredIds = roleIds.Distinct().OrderBy(id => id).ToList();
        if (desiredIds.Count > 1000)
        {
            return (null, "单次设置的角色数量不能超过 1000");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using OracleTransaction transaction = conn.BeginTransaction();
        try
        {
            string? userStatus = LoadUserStatus(conn, transaction, userId);
            if (userStatus is null)
            {
                transaction.Rollback();
                return (null, "用户不存在");
            }

            if (desiredIds.Count > 0
                && !string.Equals(userStatus, "valid", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return (null, "用户已停用，无法分配角色");
            }

            Dictionary<int, string> statuses = LoadRoleStatuses(conn, transaction, desiredIds);
            foreach (int roleId in desiredIds)
            {
                if (!statuses.TryGetValue(roleId, out string? status))
                {
                    transaction.Rollback();
                    return (null, $"角色 {roleId} 不存在");
                }

                if (!string.Equals(status, "valid", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    return (null, $"角色 {roleId} 已停用，无法分配");
                }
            }

            HashSet<int> currentIds = LoadAssignedRoleIds(conn, transaction, userId);
            List<int> idsToDelete = currentIds.Except(desiredIds).ToList();
            List<int> idsToInsert = desiredIds.Except(currentIds).ToList();
            DeleteAssignments(conn, transaction, userId, idsToDelete);
            InsertAssignments(conn, transaction, userId, idsToInsert);
            transaction.Commit();

            return (desiredIds.Select(roleId => new UserRole { UserId = userId, RoleId = roleId }).ToList(), null);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string? LoadUserStatus(OracleConnection conn, OracleTransaction transaction, int userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT STATUS FROM SYS_USER WHERE USER_ID = :userId";
        cmd.Parameters.Add(new OracleParameter("userId", userId));
        object? value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static Dictionary<int, string> LoadRoleStatuses(
        OracleConnection conn,
        OracleTransaction transaction,
        IReadOnlyList<int> roleIds)
    {
        var result = new Dictionary<int, string>();
        if (roleIds.Count == 0)
        {
            return result;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.BindByName = true;
        var binds = new List<string>();
        for (int index = 0; index < roleIds.Count; index++)
        {
            string name = $"roleId{index}";
            binds.Add($":{name}");
            cmd.Parameters.Add(new OracleParameter(name, roleIds[index]));
        }

        cmd.CommandText = $"SELECT ROLE_ID, STATUS FROM SYS_ROLE WHERE ROLE_ID IN ({string.Join(", ", binds)})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[Convert.ToInt32(reader.GetValue(0))] = reader.GetString(1);
        }

        return result;
    }

    private static HashSet<int> LoadAssignedRoleIds(
        OracleConnection conn,
        OracleTransaction transaction,
        int userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT ROLE_ID FROM SYS_USER_ROLE WHERE USER_ID = :userId FOR UPDATE";
        cmd.Parameters.Add(new OracleParameter("userId", userId));
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
        int userId,
        IReadOnlyList<int> roleIds)
    {
        if (roleIds.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.ArrayBindCount = roleIds.Count;
        cmd.CommandText = "DELETE FROM SYS_USER_ROLE WHERE USER_ID = :userId AND ROLE_ID = :roleId";
        cmd.Parameters.Add("userId", OracleDbType.Int32).Value = Enumerable.Repeat(userId, roleIds.Count).ToArray();
        cmd.Parameters.Add("roleId", OracleDbType.Int32).Value = roleIds.ToArray();
        cmd.ExecuteNonQuery();
    }

    private static void InsertAssignments(
        OracleConnection conn,
        OracleTransaction transaction,
        int userId,
        IReadOnlyList<int> roleIds)
    {
        if (roleIds.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.ArrayBindCount = roleIds.Count;
        cmd.CommandText = "INSERT INTO SYS_USER_ROLE (USER_ID, ROLE_ID) VALUES (:userId, :roleId)";
        cmd.Parameters.Add("userId", OracleDbType.Int32).Value = Enumerable.Repeat(userId, roleIds.Count).ToArray();
        cmd.Parameters.Add("roleId", OracleDbType.Int32).Value = roleIds.ToArray();
        cmd.ExecuteNonQuery();
    }
}
