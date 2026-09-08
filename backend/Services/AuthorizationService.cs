using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>统一授权结果。HTTP 固定 200，Code 用于构造业务响应。</summary>
public sealed record AuthResult
{
    public bool Ok { get; init; }
    public int Code { get; init; }
    public string? Message { get; init; }
    public CurrentUser? User { get; init; }

    public static AuthResult Success(CurrentUser user) => new() { Ok = true, Code = 200, User = user };
    public static AuthResult Unauthorized(string? message = null) =>
        new() { Ok = false, Code = 401, Message = message ?? "未登录或登录已失效" };
    public static AuthResult Forbidden(string? message = null) =>
        new() { Ok = false, Code = 403, Message = message ?? "没有权限访问该接口" };

    public ApiResponse ToApiResponse() => new()
    {
        Code = (ApiResponse.CodeEnum)Code,
        Message = Message ?? string.Empty,
        Data = null!,
    };
}

/// <summary>按稳定权限码执行登录和授权检查。</summary>
public class AuthorizationService(UserContextService userContext)
{
    public AuthResult RequireLogin(string? employeeNo)
    {
        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            return AuthResult.Unauthorized();
        }

        CurrentUser? user = userContext.Resolve(employeeNo);
        return user is null
            ? AuthResult.Unauthorized("登录状态无效")
            : AuthResult.Success(user);
    }

    public AuthResult RequirePermission(string? employeeNo, PermissionCode permissionCode)
    {
        AuthResult login = RequireLogin(employeeNo);
        if (!login.Ok)
        {
            return login;
        }

        return login.User!.HasPermission(permissionCode)
            ? login
            : AuthResult.Forbidden();
    }

    public AuthResult RequireAnyPermission(string? employeeNo, params PermissionCode[] permissionCodes)
    {
        AuthResult login = RequireLogin(employeeNo);
        if (!login.Ok)
        {
            return login;
        }

        return login.User!.HasAnyPermission(permissionCodes)
            ? login
            : AuthResult.Forbidden();
    }
}
