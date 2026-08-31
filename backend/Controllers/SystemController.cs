using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 系统管理接口（E 模块）。当前用户权限查询仅要求登录，其余管理接口要求系统管理员。
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
            var currentUser = userContext.Resolve(User.GetEmployeeNo());
            if (currentUser is null)
            {
                return Ok(new CurrentAccessResponse
                {
                    Code = CurrentAccessResponse.CodeEnum._401Enum,
                    Message = "未登录、登录已失效或账号不可用",
                    Data = null!,
                });
            }

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
                    Roles = currentUser.RoleNames.Distinct(StringComparer.Ordinal)
                        .OrderBy(role => role, StringComparer.Ordinal).ToList(),
                    Permissions = permissionService.GetEffectivePermissions(currentUser),
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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        [FromQuery(Name = "resource")] string? resource,
        [FromQuery(Name = "action")] string? action)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = permissionService.List(currentPage, size, permissionId, resource, action);

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addPermissionData")]
    public IActionResult AddPermission([FromBody] PermissionCreateRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new PermissionResponse
            {
                Code = PermissionResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (perm, error) = permissionService.Create(request);
        if (error is not null)
        {
            return Ok(new PermissionResponse
            {
                Code = PermissionResponse.CodeEnum._409Enum,
                Message = error,
                Data = null!,
            });
        }

        return Ok(new PermissionResponse
        {
            Code = PermissionResponse.CodeEnum._200Enum,
            Message = "新增成功",
            Data = perm,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updatePermissionData")]
    public IActionResult UpdatePermission([FromBody] PermissionUpdateRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new PermissionResponse
            {
                Code = PermissionResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (perm, error) = permissionService.Update(request);
        if (error is not null)
        {
            var code = error == "权限不存在"
                ? PermissionResponse.CodeEnum._404Enum
                : PermissionResponse.CodeEnum._409Enum;
            return Ok(new PermissionResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new PermissionResponse
        {
            Code = PermissionResponse.CodeEnum._200Enum,
            Message = "修改成功",
            Data = perm,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deletePermissionData")]
    public IActionResult DeletePermission([FromBody] PermissionDeleteRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (ok, error) = permissionService.Delete(request.PermissionId);
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
            Message = ok ? "删除成功" : "权限不存在",
            Data = null!,
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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
    [Route("addUserRole")]
    public IActionResult AddUserRole([FromBody] UserRoleAssignRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new UserRoleAssignResponse
            {
                Code = UserRoleAssignResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (userRoles, error) = userRoleService.Assign(request.UserId, request.RoleIds);
        if (error is not null)
        {
            var code = error.Contains("不存在")
                ? UserRoleAssignResponse.CodeEnum._404Enum
                : error.Contains("已停用")
                    ? UserRoleAssignResponse.CodeEnum._409Enum
                    : UserRoleAssignResponse.CodeEnum._400Enum;
            return Ok(new UserRoleAssignResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new UserRoleAssignResponse
        {
            Code = UserRoleAssignResponse.CodeEnum._200Enum,
            Message = "分配成功",
            Data = userRoles,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteUserRole")]
    public IActionResult DeleteUserRole([FromBody] UserRoleDeleteRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var ok = userRoleService.Delete(request.UserId, request.RoleId);
        return Ok(new ApiResponse
        {
            Code = ok ? ApiResponse.CodeEnum._200Enum : ApiResponse.CodeEnum._404Enum,
            Message = ok ? "移除成功" : "用户角色关系不存在",
            Data = null!,
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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
    [Route("addRolePermission")]
    public IActionResult AddRolePermission([FromBody] RolePermissionAssignRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new RolePermissionAssignResponse
            {
                Code = RolePermissionAssignResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var (rolePermissions, error) = rolePermissionService.Assign(request.RoleId, request.PermissionIds);
        if (error is not null)
        {
            var code = error.Contains("不存在")
                ? RolePermissionAssignResponse.CodeEnum._404Enum
                : error.Contains("已停用")
                    ? RolePermissionAssignResponse.CodeEnum._409Enum
                    : RolePermissionAssignResponse.CodeEnum._400Enum;
            return Ok(new RolePermissionAssignResponse { Code = code, Message = error, Data = null! });
        }

        return Ok(new RolePermissionAssignResponse
        {
            Code = RolePermissionAssignResponse.CodeEnum._200Enum,
            Message = "分配成功",
            Data = rolePermissions,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteRolePermission")]
    public IActionResult DeleteRolePermission([FromBody] RolePermissionDeleteRequest? request)
    {
        if (RequireAdmin() is { } forbidden) return forbidden;

        if (request is null)
        {
            return Ok(new ApiResponse
            {
                Code = ApiResponse.CodeEnum._400Enum,
                Message = "请求体不能为空",
                Data = null!,
            });
        }

        var ok = rolePermissionService.Delete(request.RoleId, request.PermissionId);
        return Ok(new ApiResponse
        {
            Code = ok ? ApiResponse.CodeEnum._200Enum : ApiResponse.CodeEnum._404Enum,
            Message = ok ? "移除成功" : "角色权限关系不存在",
            Data = null!,
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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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
        if (RequireAdmin() is { } forbidden) return forbidden;

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

    /// <summary>要求当前用户为系统管理员；否则返回 403 响应。</summary>
    private IActionResult? RequireAdmin()
    {
        var result = authorization.RequireRole(User.GetEmployeeNo(), AuthorizationService.AdminRole);
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
            Message = result.Message ?? "仅系统管理员可访问",
            Data = null!,
        });
    }
}
