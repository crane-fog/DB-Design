using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("/api")]
public class BomVersionController(
    BomVersionService bomVersionService,
    UserContextService userContext) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listBomVersionData")]
    public IActionResult ListVersions(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "version_no")] string? versionNo,
        [FromQuery(Name = "effective_only")] bool? effectiveOnly)
    {
        if (ResolvePageReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = bomVersionService.ListVersions(currentPage, size, materialId, versionNo, effectiveOnly);
        return Ok(new BomVersionPageResponse
        {
            Code = BomVersionPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new PageResult
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records.Cast<object>().ToList(),
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getBomVersionData")]
    public IActionResult GetVersion([FromQuery(Name = "version_id")] long versionId)
    {
        if (ResolveReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var version = bomVersionService.GetVersion(versionId);
        return version is null
            ? Ok(Version(BomVersionResponse.CodeEnum._404Enum, "BOM 版本不存在", null))
            : Ok(Version(BomVersionResponse.CodeEnum._200Enum, "查询成功", version));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addBomVersionData")]
    public IActionResult AddVersion([FromBody] BomVersionCreateRequest? request)
    {
        var user = ResolveManager();
        if (user.Response is not null)
        {
            return user.Response;
        }

        if (request is null)
        {
            return Ok(Version(BomVersionResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromVersionResult(bomVersionService.AddVersion(request, user.User!.UserId), "创建成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateBomVersionData")]
    public IActionResult UpdateVersion([FromBody] BomVersionUpdateRequest? request)
    {
        if (ResolveVersionManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Version(BomVersionResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromVersionResult(bomVersionService.UpdateVersion(request), "修改成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteBomVersionData")]
    public IActionResult DeleteVersion([FromBody] BomVersionDeleteRequest? request)
    {
        if (ResolveCommonManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._400Enum, "请求体不能为空"));
        }

        var result = bomVersionService.DeleteVersion(request.VersionId);
        return result.Ok
            ? Ok(Common(ApiResponse.CodeEnum._200Enum, "删除成功"))
            : Ok(Common((ApiResponse.CodeEnum)(int)result.Error, result.ErrorMessage ?? "删除失败"));
    }

    private IActionResult? ResolveReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Version(BomVersionResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialReader)
        {
            return Ok(Version(BomVersionResponse.CodeEnum._403Enum, "无权访问 BOM 版本", null));
        }

        return null;
    }

    private IActionResult? ResolvePageReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(new BomVersionPageResponse
            {
                Code = BomVersionPageResponse.CodeEnum._401Enum,
                Message = "登录状态无效",
                Data = null!,
            });
        }

        if (!user.IsMaterialReader)
        {
            return Ok(new BomVersionPageResponse
            {
                Code = BomVersionPageResponse.CodeEnum._403Enum,
                Message = "无权访问 BOM 版本",
                Data = null!,
            });
        }

        return null;
    }

    private IActionResult? ResolveVersionManagerOrForbidden() => ResolveManager().Response;

    private IActionResult? ResolveCommonManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._401Enum, "登录状态无效"));
        }

        if (!user.IsMaterialManager)
        {
            return Ok(Common(ApiResponse.CodeEnum._403Enum, "无权维护 BOM 版本"));
        }

        return null;
    }

    private (CurrentUser? User, IActionResult? Response) ResolveManager()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return (null, Ok(Version(BomVersionResponse.CodeEnum._401Enum, "登录状态无效", null)));
        }

        if (!user.IsMaterialManager)
        {
            return (null, Ok(Version(BomVersionResponse.CodeEnum._403Enum, "无权维护 BOM 版本", null)));
        }

        return (user, null);
    }

    private static IActionResult FromVersionResult(BomBusinessResult<BomVersion> result, string successMessage)
    {
        if (result.Ok)
        {
            return new OkObjectResult(Version(BomVersionResponse.CodeEnum._200Enum, successMessage, result.Data));
        }

        return new OkObjectResult(Version(
            (BomVersionResponse.CodeEnum)(int)result.Error,
            result.ErrorMessage ?? "操作失败",
            null));
    }

    private static BomVersionResponse Version(
        BomVersionResponse.CodeEnum code,
        string message,
        BomVersion? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static ApiResponse Common(ApiResponse.CodeEnum code, string message) => new()
    {
        Code = code,
        Message = message,
        Data = null!,
    };
}
