using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>
/// 统一授权结果，供 Controller 判断是否放行。
/// Ok=true 表示通过；Ok=false 时 Code/Message 对应业务状态码和提示。
/// </summary>
public sealed record AuthResult
{
    public bool Ok { get; init; }
    public int Code { get; init; }
    public string? Message { get; init; }

    public static AuthResult Success() => new() { Ok = true, Code = 200 };
    public static AuthResult Unauthorized(string? message = null) =>
        new() { Ok = false, Code = 401, Message = message ?? "未登录或登录已失效" };
    public static AuthResult Forbidden(string? message = null) =>
        new() { Ok = false, Code = 403, Message = message ?? "没有权限访问该接口" };

    /// <summary>
    /// 将鉴权结果转换为统一 ApiResponse，用于不需要特定响应类型的场景。
    /// </summary>
    public ApiResponse ToApiResponse() => new()
    {
        Code = (ApiResponse.CodeEnum)Code,
        Message = Message ?? "",
        Data = null,
    };
}

/// <summary>
/// E 模块提供的公共鉴权设施。
/// 其他模块通过依赖注入引用，替代各自重复的 ResolveManagerOrForbidden() 逻辑。
/// 用法：var result = authorization.RequireRole(employeeNo, "生产管理员", "系统管理员");
/// </summary>
public class AuthorizationService(UserContextService userContext)
{
    /// <summary>系统管理员角色名：拥有全部权限。</summary>
    public const string AdminRole = "系统管理员";

    /// <summary>生产管理员角色名：可操作生产相关模块。</summary>
    public const string ProductionManagerRole = "生产管理员";

    /// <summary>质量管理员角色名：可操作质量追溯相关模块。</summary>
    public const string QualityManagerRole = "质量管理员";

    /// <summary>外部客户角色名：只能访问自己的外部订单。</summary>
    public const string ExternalCustomerRole = "外部客户";

    /// <summary>
    /// 要求当前用户已登录，且拥有指定角色之一。
    /// 系统管理员自动通过所有角色检查。
    /// </summary>
    /// <param name="employeeNo">从 JWT 解析的员工工号（User.GetEmployeeNo()）。</param>
    /// <param name="allowedRoles">允许的角色名称列表。传空值或空数组表示仅要求登录。</param>
    /// <returns>授权结果。Ok=true 表示通过。</returns>
    public AuthResult RequireRole(string? employeeNo, params string[] allowedRoles)
    {
        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            return AuthResult.Unauthorized();
        }

        var user = userContext.Resolve(employeeNo);
        if (user is null)
        {
            return AuthResult.Unauthorized("登录状态无效");
        }

        // 系统管理员拥有全部权限
        if (user.RoleNames.Contains(AdminRole))
        {
            return AuthResult.Success();
        }

        // 不指定角色时仅要求登录
        if (allowedRoles is null || allowedRoles.Length == 0)
        {
            return AuthResult.Success();
        }

        if (allowedRoles.Any(role => user.RoleNames.Contains(role)))
        {
            return AuthResult.Success();
        }

        return AuthResult.Forbidden("没有权限访问该接口");
    }

    /// <summary>
    /// 仅要求已登录，不检查特定角色。
    /// </summary>
    public AuthResult RequireLogin(string? employeeNo) => RequireRole(employeeNo);
}
