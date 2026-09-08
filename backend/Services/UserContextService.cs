using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>
/// 从登录态解析出的当前用户。角色仅用于展示，所有授权判断都基于有效权限码。
/// </summary>
public sealed record CurrentUser(
    long UserId,
    string EmployeeNo,
    string UserName,
    IReadOnlyList<RoleBrief> Roles,
    IReadOnlySet<PermissionCode> PermissionCodes)
{
    public bool HasPermission(PermissionCode permissionCode) => PermissionCodes.Contains(permissionCode);

    public bool HasAnyPermission(params PermissionCode[] permissionCodes) =>
        permissionCodes.Any(PermissionCodes.Contains);
}

/// <summary>
/// 根据 JWT 中的 employee_no 一次加载用户、全部有效角色及其有效权限并集。
/// </summary>
public class UserContextService(string connString)
{
    public CurrentUser? Resolve(string? employeeNo)
    {
        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            return null;
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT U.USER_ID,
                                   U.EMPLOYEE_NO,
                                   U.USER_NAME,
                                   R.ROLE_ID,
                                   R.ROLE_NAME,
                                   R.STATUS,
                                   P.PERMISSION_CODE
                            FROM SYS_USER U
                            LEFT JOIN SYS_USER_ROLE UR ON UR.USER_ID = U.USER_ID
                            LEFT JOIN SYS_ROLE R
                              ON R.ROLE_ID = UR.ROLE_ID
                             AND R.STATUS = 'valid'
                            LEFT JOIN SYS_ROLE_PERMISSION RP ON RP.ROLE_ID = R.ROLE_ID
                            LEFT JOIN SYS_PERMISSION P
                              ON P.PERMISSION_ID = RP.PERMISSION_ID
                             AND P.STATUS = 'valid'
                            WHERE U.EMPLOYEE_NO = :employeeNo
                              AND U.STATUS = 'valid'
                            ORDER BY R.ROLE_ID, P.PERMISSION_CODE";
        cmd.Parameters.Add(new OracleParameter("employeeNo", employeeNo.Trim()));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        long userId = Convert.ToInt64(reader.GetValue(0));
        string storedEmployeeNo = reader.GetString(1);
        string userName = reader.GetString(2);
        var roles = new Dictionary<int, RoleBrief>();
        var permissions = new HashSet<PermissionCode>();

        do
        {
            if (!reader.IsDBNull(3))
            {
                int roleId = Convert.ToInt32(reader.GetValue(3));
                roles.TryAdd(roleId, new RoleBrief
                {
                    RoleId = roleId,
                    RoleName = reader.GetString(4),
                    Status = RoleBrief.StatusEnum.ValidEnum,
                });
            }

            if (!reader.IsDBNull(6))
            {
                string databaseCode = reader.GetString(6);
                if (!PermissionCodeMapper.TryParse(databaseCode, out PermissionCode permissionCode))
                {
                    throw new InvalidOperationException($"数据库包含 API 契约未定义的权限码：{databaseCode}");
                }

                permissions.Add(permissionCode);
            }
        }
        while (reader.Read());

        return new CurrentUser(
            userId,
            storedEmployeeNo,
            userName,
            roles.Values.OrderBy(role => role.RoleId).ToList(),
            permissions);
    }
}
