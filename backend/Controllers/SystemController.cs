using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 系统管理接口（E 模块）。各接口分别检查稳定权限码。
/// HTTP 固定 200，业务状态通过响应体 code 表达。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class SystemController(
    AuthorizationService authorization,
    UserContextService userContext,
    UserService userService,
    RoleService roleService,
    PermissionService permissionService,
    UserRoleService userRoleService,
    RolePermissionService rolePermissionService,
    LoginLogService loginLogService,
    OperationLogService operationLogService) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("getCurrentAccess")]
    public IActionResult GetCurrentAccess()
    {
        Response.Headers["Cache-Control"] = "no-store";

        try
        {
            AuthResult auth = authorization.RequireLogin(User.GetEmployeeNo());
            if (!auth.Ok)
            {
                return Ok(new CurrentAccessResponse
                {
                    Code = CurrentAccessResponse.CodeEnum._401Enum,
                    Message = auth.Message ?? "未登录、登录已失效或账号不可用",
                    Data = null!,
                });
            }

            CurrentUser currentUser = auth.User!;

            return Ok(new CurrentAccessResponse
            {
                Code = CurrentAccessResponse.CodeEnum._200Enum,
                Message = "查询成功",
                Data = new CurrentAccessData
                {
                    CurrentUser = new CurrentAccessUser
                    {
                        UserId = currentUser.UserId,
                        EmployeeNo = currentUser.EmployeeNo,
                        UserName = currentUser.UserName,
                    },
                    Roles = currentUser.Roles.ToList(),
                    PermissionCodes = currentUser.PermissionCodes.OrderBy(code => code).ToList(),
                },
            });
        }
        catch (OracleException)
        {
            return Ok(new CurrentAccessResponse
            {
                Code = CurrentAccessResponse.CodeEnum._500Enum,
                Message = "查询当前用户权限失败",
                Data = null!,
            });
        }
    }

    // ==================== 用户管理 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listUserData")]
    public IActionResult ListUser(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "user_id")] int? userId,
        [FromQuery(Name = "employee_no")] string? employeeNo,
        [FromQuery(Name = "user_name")] string? userName,
        [FromQuery(Name = "status")] string? status)
    {
        if (RequirePermission(PermissionCode.SystemUserViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = userService.List(currentPage, size, userId, employeeNo, userName, status);

        return Ok(new UserPageResponse
        {
            Code = UserPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getUserData")]
    public IActionResult GetUser([FromQuery(Name = "user_id")] int userId)
    {
        if (RequirePermission(PermissionCode.SystemUserViewEnum) is { } forbidden) return forbidden;

        var user = userService.Get(userId);
        if (user is null)
        {
            return Ok(new UserResponse
            {
                Code = UserResponse.CodeEnum._404Enum,
                Message = "用户不存在",
                Data = null!,
            });
        }

        return Ok(new UserResponse
        {
            Code = UserResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = user,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addUserData")]
    public IActionResult AddUser([FromBody] UserCreateRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemUserCreateEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new UserResponse
            {
                Code = UserResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (user, error) = userService.Create(request);
        if (error is not null)
        {
            return Ok(new UserResponse
            {
                Code = UserResponse.CodeEnum._409Enum,
                Message = error,
                Data = null!,
            });
        }

        return Ok(new UserResponse
        {
            Code = UserResponse.CodeEnum._200Enum,
            Message = "新增成功",
            Data = user,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateUserData")]
    public IActionResult UpdateUser([FromBody] UserUpdateRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemUserUpdateEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new UserResponse
            {
                Code = UserResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (user, error) = userService.Update(request);
        if (error is not null)
        {
            var code = error switch
            {
                "用户不存在" => UserResponse.CodeEnum._404Enum,
                "工号已存在" => UserResponse.CodeEnum._409Enum,
                _ => UserResponse.CodeEnum._400Enum,
            };
            return Ok(new UserResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new UserResponse
        {
            Code = UserResponse.CodeEnum._200Enum,
            Message = "修改成功",
            Data = user,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteUserData")]
    public IActionResult DeleteUser([FromBody] UserDeleteRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemUserDeleteEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (ok, error) = userService.Delete(request.UserId);
        if (error is not null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._409Enum,
                Message = error,
                Data = null!,
            });
        }

        return Ok(new ApiResponse
        {
            Code = ok ? ApiResponse.CodeEnum._200Enum : ApiResponse.CodeEnum._404Enum,
            Message = ok ? "删除成功" : "用户不存在",
            Data = null!,
        });
    }

    // ==================== 角色管理 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listRoleData")]
    public IActionResult ListRole(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "role_id")] int? roleId,
        [FromQuery(Name = "role_name")] string? roleName,
        [FromQuery(Name = "status")] string? status)
    {
        if (RequirePermission(PermissionCode.SystemRoleViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = roleService.List(currentPage, size, roleId, roleName, status);

        return Ok(new RolePageResponse
        {
            Code = RolePageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getRoleData")]
    public IActionResult GetRole([FromQuery(Name = "role_id")] int roleId)
    {
        if (RequirePermission(PermissionCode.SystemRoleViewEnum) is { } forbidden) return forbidden;

        var role = roleService.Get(roleId);
        if (role is null)
        {
            return Ok(new RoleResponse
            {
                Code = RoleResponse.CodeEnum._404Enum,
                Message = "角色不存在",
                Data = null!,
            });
        }

        return Ok(new RoleResponse
        {
            Code = RoleResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = role,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addRoleData")]
    public IActionResult AddRole([FromBody] RoleCreateRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemRoleCreateEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new RoleResponse
            {
                Code = RoleResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (role, error) = roleService.Create(request);
        if (error is not null)
        {
            return Ok(new RoleResponse
            {
                Code = RoleResponse.CodeEnum._409Enum,
                Message = error,
                Data = null!,
            });
        }

        return Ok(new RoleResponse
        {
            Code = RoleResponse.CodeEnum._200Enum,
            Message = "新增成功",
            Data = role,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateRoleData")]
    public IActionResult UpdateRole([FromBody] RoleUpdateRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemRoleUpdateEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new RoleResponse
            {
                Code = RoleResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (role, error) = roleService.Update(request);
        if (error is not null)
        {
            var code = error == "角色不存在"
                ? RoleResponse.CodeEnum._404Enum
                : RoleResponse.CodeEnum._409Enum;
            return Ok(new RoleResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new RoleResponse
        {
            Code = RoleResponse.CodeEnum._200Enum,
            Message = "修改成功",
            Data = role,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteRoleData")]
    public IActionResult DeleteRole([FromBody] RoleDeleteRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemRoleDeleteEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (ok, error) = roleService.Delete(request.RoleId);
        if (error is not null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._409Enum,
                Message = error,
                Data = null!,
            });
        }

        return Ok(new ApiResponse
        {
            Code = ok ? ApiResponse.CodeEnum._200Enum : ApiResponse.CodeEnum._404Enum,
            Message = ok ? "删除成功" : "角色不存在",
            Data = null!,
        });
    }

    // ==================== 权限管理 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listPermissionData")]
    public IActionResult ListPermission(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "permission_id")] int? permissionId,
        [FromQuery(Name = "permission_code")] string? permissionCode,
        [FromQuery(Name = "module_name")] string? moduleName,
        [FromQuery(Name = "resource_name")] string? resourceName,
        [FromQuery(Name = "action_name")] string? actionName)
    {
        if (RequirePermission(PermissionCode.SystemPermissionViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = permissionService.List(
            currentPage,
            size,
            permissionId,
            permissionCode,
            moduleName,
            resourceName,
            actionName);

        return Ok(new PermissionPageResponse
        {
            Code = PermissionPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getPermissionData")]
    public IActionResult GetPermission([FromQuery(Name = "permission_id")] int permissionId)
    {
        if (RequirePermission(PermissionCode.SystemPermissionViewEnum) is { } forbidden) return forbidden;

        var perm = permissionService.Get(permissionId);
        if (perm is null)
        {
            return Ok(new PermissionResponse
            {
                Code = PermissionResponse.CodeEnum._404Enum,
                Message = "权限不存在",
                Data = null!,
            });
        }

        return Ok(new PermissionResponse
        {
            Code = PermissionResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = perm,
        });
    }

    // ==================== 用户-角色分配 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listUserRoleData")]
    public IActionResult ListUserRole(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "user_id")] int? userId,
        [FromQuery(Name = "role_id")] int? roleId)
    {
        if (RequirePermission(PermissionCode.SystemUserAssignRoleEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = userRoleService.List(currentPage, size, userId, roleId);

        return Ok(new UserRolePageResponse
        {
            Code = UserRolePageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("setUserRoles")]
    public IActionResult SetUserRoles([FromBody] UserRoleSetRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemUserAssignRoleEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new UserRoleSetResponse
            {
                Code = UserRoleSetResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (userRoles, error) = userRoleService.Set(request.UserId, request.RoleIds);
        if (error is not null)
        {
            var code = error.Contains("不存在")
                ? UserRoleSetResponse.CodeEnum._404Enum
                : error.Contains("已停用")
                    ? UserRoleSetResponse.CodeEnum._409Enum
                    : UserRoleSetResponse.CodeEnum._400Enum;
            return Ok(new UserRoleSetResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new UserRoleSetResponse
        {
            Code = UserRoleSetResponse.CodeEnum._200Enum,
            Message = "设置成功",
            Data = userRoles,
        });
    }

    // ==================== 角色-权限分配 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listRolePermissionData")]
    public IActionResult ListRolePermission(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "role_id")] int? roleId,
        [FromQuery(Name = "permission_id")] int? permissionId)
    {
        if (RequirePermission(PermissionCode.SystemRoleAssignPermissionEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = rolePermissionService.List(currentPage, size, roleId, permissionId);

        return Ok(new RolePermissionPageResponse
        {
            Code = RolePermissionPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("setRolePermissions")]
    public IActionResult SetRolePermissions([FromBody] RolePermissionSetRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemRoleAssignPermissionEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new RolePermissionSetResponse
            {
                Code = RolePermissionSetResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (rolePermissions, error) = rolePermissionService.Set(request.RoleId, request.PermissionIds);
        if (error is not null)
        {
            var code = error.Contains("不存在")
                ? RolePermissionSetResponse.CodeEnum._404Enum
                : error.Contains("已停用")
                    ? RolePermissionSetResponse.CodeEnum._409Enum
                    : RolePermissionSetResponse.CodeEnum._400Enum;
            return Ok(new RolePermissionSetResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new RolePermissionSetResponse
        {
            Code = RolePermissionSetResponse.CodeEnum._200Enum,
            Message = "设置成功",
            Data = rolePermissions,
        });
    }

    // ==================== 登录日志 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listLoginRecordData")]
    public IActionResult ListLoginLog(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "user_id")] int? userId,
        [FromQuery(Name = "result")] string? result,
        [FromQuery(Name = "start_time")] DateTime? startTime,
        [FromQuery(Name = "end_time")] DateTime? endTime)
    {
        if (RequirePermission(PermissionCode.SystemAuditLoginViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = loginLogService.List(currentPage, size, userId, result, startTime, endTime);

        return Ok(new LoginLogPageResponse
        {
            Code = LoginLogPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    // ==================== 操作日志 ====================

    [HttpGet]
    [Produces("application/json")]
    [Route("listOperationLogData")]
    public IActionResult ListOperationLog(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "module")] string? module,
        [FromQuery(Name = "action")] string? action,
        [FromQuery(Name = "operator_id")] int? operatorId,
        [FromQuery(Name = "start_time")] DateTime? startTime,
        [FromQuery(Name = "end_time")] DateTime? endTime)
    {
        if (RequirePermission(PermissionCode.SystemAuditOperationViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = operationLogService.List(currentPage, size, module, action, operatorId, startTime, endTime);

        return Ok(new OperationLogPageResponse
        {
            Code = OperationLogPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new Dictionary<string, object>
            {
                ["total"] = total,
                ["page"] = currentPage,
                ["page_size"] = size,
                ["records"] = records,
            },
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addOperationLogData")]
    public IActionResult AddOperationLog([FromBody] OperationLogCreateRequest? request)
    {
        if (RequirePermission(PermissionCode.SystemAuditOperationCreateEnum) is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new OperationLogResponse
            {
                Code = OperationLogResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        // 审计日志的操作人与来源 IP 必须取服务端上下文，不能信任客户端声明，
        // 否则调用者可伪造“由其他用户、其他 IP 执行”的审计记录。
        var currentUser = userContext.Resolve(User.GetEmployeeNo());
        if (currentUser is null)
        {
            // 无法解析出有效操作人（用户已停用/不存在）：不写入日志。
            // 若写入 operator_id=0，会因 OPERATION_LOG.OPERATOR_ID 外键引用
            // sys_user(user_id)（且 0 号用户不存在）触发 ORA-02291 导致 HTTP 500。
            return Ok(new OperationLogResponse
            {
                Code = OperationLogResponse.CodeEnum._401Enum,
                Message = "登录状态无效，无法记录操作日志",
                Data = null!,
            });
        }

        request.OperatorId = Convert.ToInt32(currentUser.UserId);
        request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        var log = operationLogService.Write(request);
        return Ok(new OperationLogResponse
        {
            Code = OperationLogResponse.CodeEnum._200Enum,
            Message = "新增成功",
            Data = log,
        });
    }

    // ==================== 私有辅助方法 ====================

    private IActionResult? RequirePermission(PermissionCode permissionCode)
    {
        AuthResult result = authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);
        if (result.Ok) return null;

        if (result.Code == 401)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._401Enum,
                Message = result.Message ?? "登录状态无效",
                Data = null!,
            });
        }

        return Ok(new ApiResponse
        {
            Code = ApiResponse.CodeEnum._403Enum,
            Message = result.Message ?? "没有权限访问该接口",
            Data = null!,
        });
    }
}
