using Backend.Filters;
using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("/api")]
public class BomController(
    BomService bomService,
    UserContextService userContext) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listBomData")]
    public IActionResult ListBoms(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "version_id")] long versionId,
        [FromQuery(Name = "parent_material_id")] long? parentMaterialId,
        [FromQuery(Name = "child_material_id")] long? childMaterialId)
    {
        if (ResolvePageReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (versionId <= 0)
        {
            return Ok(new BomPageResponse
            {
                Code = BomPageResponse.CodeEnum._400Enum,
                Message = "版本编号不能为空",
                Data = null!,
            });
        }

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = bomService.ListBoms(currentPage, size, versionId, parentMaterialId, childMaterialId);
        return Ok(new BomPageResponse
        {
            Code = BomPageResponse.CodeEnum._200Enum,
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
    [Route("getBomData")]
    public IActionResult GetBom([FromQuery(Name = "bom_id")] long bomId)
    {
        if (ResolveReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var bom = bomService.GetBom(bomId);
        return bom is null
            ? Ok(Bom(BomResponse.CodeEnum._404Enum, "BOM 明细不存在", null))
            : Ok(Bom(BomResponse.CodeEnum._200Enum, "查询成功", bom));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addBomData")]
    [RequireJsonFields("loss_rate")]
    public IActionResult AddBom([FromBody] BomCreateRequest? request)
    {
        if (ResolveBomManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Bom(BomResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromBomResult(bomService.AddBom(request), "创建成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateBomData")]
    [RequireJsonFields("loss_rate")]
    public IActionResult UpdateBom([FromBody] BomUpdateRequest? request)
    {
        if (ResolveBomManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Bom(BomResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromBomResult(bomService.UpdateBom(request), "修改成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteBomData")]
    public IActionResult DeleteBom([FromBody] BomDeleteRequest? request)
    {
        if (ResolveCommonManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._400Enum, "请求体不能为空"));
        }

        var result = bomService.DeleteBom(request.BomId);
        return result.Ok
            ? Ok(Common(ApiResponse.CodeEnum._200Enum, "删除成功"))
            : Ok(Common((ApiResponse.CodeEnum)(int)result.Error, result.ErrorMessage ?? "删除失败"));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("checkBomCycleDependency")]
    public IActionResult CheckCycle([FromBody] BomCycleCheckRequest? request)
    {
        if (ResolveCycleManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Cycle(BomCycleCheckResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return Ok(Cycle(BomCycleCheckResponse.CodeEnum._200Enum, "检查成功", bomService.CheckCycle(request)));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getBomTreeData")]
    public IActionResult GetBomTree(
        [FromQuery(Name = "material_id")] long materialId,
        [FromQuery(Name = "version_id")] long versionId)
    {
        if (ResolveTreeReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var result = bomService.GetBomTree(materialId, versionId);
        return Ok(Tree(
            result.Ok ? BomTreeResponse.CodeEnum._200Enum : (BomTreeResponse.CodeEnum)(int)result.Error,
            result.Ok ? "查询成功" : result.ErrorMessage ?? "查询失败",
            result.Data));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getReverseTraceData")]
    public IActionResult GetReverseTrace(
        [FromQuery(Name = "material_id")] long materialId,
        [FromQuery(Name = "include_history")] bool includeHistory = false)
    {
        if (ResolveReverseReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var result = bomService.GetReverseTrace(materialId, includeHistory);
        return Ok(Reverse(
            result.Ok ? ReverseTraceResponse.CodeEnum._200Enum : (ReverseTraceResponse.CodeEnum)(int)result.Error,
            result.Ok ? "查询成功" : result.ErrorMessage ?? "查询失败",
            result.Data));
    }

    private IActionResult? ResolveReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Bom(BomResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialReader)
        {
            return Ok(Bom(BomResponse.CodeEnum._403Enum, "无权访问 BOM 明细", null));
        }

        return null;
    }

    private IActionResult? ResolveTreeReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Tree(BomTreeResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialReader)
        {
            return Ok(Tree(BomTreeResponse.CodeEnum._403Enum, "无权查询 BOM 层级树", null));
        }

        return null;
    }

    private IActionResult? ResolveReverseReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Reverse(ReverseTraceResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialReader)
        {
            return Ok(Reverse(ReverseTraceResponse.CodeEnum._403Enum, "无权查询反向追溯", null));
        }

        return null;
    }

    private IActionResult? ResolvePageReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(new BomPageResponse
            {
                Code = BomPageResponse.CodeEnum._401Enum,
                Message = "登录状态无效",
                Data = null!,
            });
        }

        if (!user.IsMaterialReader)
        {
            return Ok(new BomPageResponse
            {
                Code = BomPageResponse.CodeEnum._403Enum,
                Message = "无权访问 BOM 明细",
                Data = null!,
            });
        }

        return null;
    }

    private IActionResult? ResolveBomManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Bom(BomResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialManager)
        {
            return Ok(Bom(BomResponse.CodeEnum._403Enum, "无权维护 BOM 明细", null));
        }

        return null;
    }

    private IActionResult? ResolveCycleManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Cycle(BomCycleCheckResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialManager)
        {
            return Ok(Cycle(BomCycleCheckResponse.CodeEnum._403Enum, "无权维护 BOM 明细", null));
        }

        return null;
    }

    private IActionResult? ResolveCommonManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._401Enum, "登录状态无效"));
        }

        if (!user.IsMaterialManager)
        {
            return Ok(Common(ApiResponse.CodeEnum._403Enum, "无权维护 BOM 明细"));
        }

        return null;
    }

    private static IActionResult FromBomResult(BomBusinessResult<Bom> result, string successMessage)
    {
        if (result.Ok)
        {
            return new OkObjectResult(Bom(BomResponse.CodeEnum._200Enum, successMessage, result.Data));
        }

        return new OkObjectResult(Bom(
            (BomResponse.CodeEnum)(int)result.Error,
            result.ErrorMessage ?? "操作失败",
            null));
    }

    private static BomResponse Bom(BomResponse.CodeEnum code, string message, Bom? data) => new()
    {
        Code = code,
        Message = message,
        Data = data!,
    };

    private static BomCycleCheckResponse Cycle(
        BomCycleCheckResponse.CodeEnum code,
        string message,
        BomCycleCheckResult? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static BomTreeResponse Tree(
        BomTreeResponse.CodeEnum code,
        string message,
        List<BomTreeNode>? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static ReverseTraceResponse Reverse(
        ReverseTraceResponse.CodeEnum code,
        string message,
        List<ReverseTraceItem>? data) => new()
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
