using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

/// <summary>
/// 从登录态解析出的当前用户，供 C 模块的业务 Service 填充 operator/reviewer 身份，
/// 并判断是否外部客户以实现数据自限。
/// </summary>
public sealed record CurrentUser(long UserId, string EmployeeNo, string UserName, IReadOnlyList<string> RoleNames)
{
    /// <summary>是否为外部客户角色（只能访问自己的外部订单）。</summary>
    public bool IsExternalCustomer => RoleNames.Contains("外部客户");

    /// <summary>是否具备生产/系统管理员视角（可查看全部生产、外部订单）。</summary>
    public bool IsProductionManager =>
        RoleNames.Contains("系统管理员") || RoleNames.Contains("生产管理员");

    /// <summary>是否具备库存管理视角（库存管理员、生产管理员、系统管理员）。</summary>
    public bool IsInventoryManager =>
        RoleNames.Contains("系统管理员") || RoleNames.Contains("生产管理员") || RoleNames.Contains("库存管理员");

    /// <summary>是否具备采购视角（采购员、采购主管、系统管理员）。</summary>
    public bool IsPurchaser =>
        RoleNames.Contains("系统管理员") || RoleNames.Contains("采购员") || RoleNames.Contains("采购主管");

    /// <summary>是否可查询物料主数据。</summary>
    public bool IsMaterialReader =>
        IsMaterialManager || RoleNames.Contains("采购员");

    /// <summary>是否可维护物料主数据。</summary>
    public bool IsMaterialManager =>
        RoleNames.Contains("系统管理员") || RoleNames.Contains("生产管理员");
}

/// <summary>
/// 解析 JWT 中的 employee_no claim 到数据库用户身份和角色。
/// 当前 token 仅携带 employee_no（见 AuthService.CreateToken），
/// 因此需要回查 SYS_USER / SYS_USER_ROLE / SYS_ROLE 得到 user_id 和角色。
/// </summary>
public class UserContextService(string connString)
{
    /// <summary>
    /// 根据登录工号解析当前用户；工号不存在或已停用时返回 null。
    /// </summary>
    public CurrentUser? Resolve(string? employeeNo)
    {
        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            return null;
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        long userId;
        string storedEmployeeNo;
        string userName;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT USER_ID, EMPLOYEE_NO, USER_NAME, STATUS
                                FROM SYS_USER
                                WHERE EMPLOYEE_NO = :employeeNo";
            cmd.Parameters.Add(new OracleParameter("employeeNo", employeeNo.Trim()));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var status = reader.GetString(3);
            if (string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            userId = Convert.ToInt64(reader.GetValue(0));
            storedEmployeeNo = reader.GetString(1);
            userName = reader.GetString(2);
        }

        var roleNames = new List<string>();
        using (var roleCmd = conn.CreateCommand())
        {
            // 只加载有效角色：disabled 角色不应继续用于新的授权或权限生效。
            roleCmd.CommandText = @"SELECT R.ROLE_NAME
                                    FROM SYS_USER_ROLE UR
                                    JOIN SYS_ROLE R ON R.ROLE_ID = UR.ROLE_ID
                                    WHERE UR.USER_ID = :userId
                                      AND R.STATUS = 'valid'";
            roleCmd.Parameters.Add(new OracleParameter("userId", userId));

            using var roleReader = roleCmd.ExecuteReader();
            while (roleReader.Read())
            {
                roleNames.Add(roleReader.GetString(0));
            }
        }

        return new CurrentUser(userId, storedEmployeeNo, userName, roleNames);
    }
}
