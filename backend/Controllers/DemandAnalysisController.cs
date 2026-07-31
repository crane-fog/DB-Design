using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("/api")]
public class DemandAnalysisController(
    DemandAnalysisService demandAnalysisService,
    UserContextService userContext) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("calculateLossCompensation")]
    public IActionResult CalculateLossCompensation([FromBody] LossCompensationCalculateRequest? request)
    {
        if (ResolveManagerOrForbidden(Loss) is { } forbidden) return forbidden;
        if (request is null) return Ok(Loss(LossCompensationResponse.CodeEnum._400Enum, "请求体不能为空", null));
        var result = demandAnalysisService.CalculateLossCompensation(request);
        return Ok(Loss(result.Ok ? LossCompensationResponse.CodeEnum._200Enum : (LossCompensationResponse.CodeEnum)(int)result.Error,
            result.Ok ? "计算成功" : result.ErrorMessage ?? "计算失败", result.Data));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("calculateProductCost")]
    public IActionResult CalculateProductCost([FromBody] ProductCostCalculateRequest? request)
    {
        if (ResolveCostReaderOrForbidden() is { } forbidden) return forbidden;
        if (request is null) return Ok(Cost(ProductCostResponse.CodeEnum._400Enum, "请求体不能为空", null));
        var result = demandAnalysisService.CalculateProductCost(request);
        return Ok(Cost(result.Ok ? ProductCostResponse.CodeEnum._200Enum : (ProductCostResponse.CodeEnum)(int)result.Error,
            result.Ok ? "计算成功" : result.ErrorMessage ?? "计算失败", result.Data));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addRequirementAnalysis")]
    public IActionResult AddRequirementAnalysis([FromBody] DemandAnalysisCreateRequest? request)
    {
        if (ResolveAnalysisManagerOrForbidden() is { } forbidden) return forbidden;
        if (request is null) return Ok(Analysis(DemandAnalysisResponse.CodeEnum._400Enum, "请求体不能为空", null));
        var result = demandAnalysisService.AddRequirementAnalysis(request);
        return Ok(Analysis(result.Ok ? DemandAnalysisResponse.CodeEnum._200Enum : (DemandAnalysisResponse.CodeEnum)(int)result.Error,
            result.Ok ? "保存成功" : result.ErrorMessage ?? "保存失败", result.Data));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getRequirementAnalysis")]
    public IActionResult GetRequirementAnalysis(
        [FromQuery(Name = "analysis_id")] long? analysisId,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "version_id")] long? versionId)
    {
        if (ResolveAnalysisReaderOrForbidden() is { } forbidden) return forbidden;
        if (analysisId is <= 0 || materialId is <= 0 || versionId is <= 0)
            return Ok(Analysis(DemandAnalysisResponse.CodeEnum._400Enum, "查询编号必须大于 0", null));
        var analysis = demandAnalysisService.GetRequirementAnalysis(analysisId, materialId, versionId);
        return Ok(analysis is null ? Analysis(DemandAnalysisResponse.CodeEnum._404Enum, "需求分析记录不存在", null)
            : Analysis(DemandAnalysisResponse.CodeEnum._200Enum, "查询成功", analysis));
    }

    private IActionResult? ResolveCostReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null) return Ok(Cost(ProductCostResponse.CodeEnum._401Enum, "登录状态无效", null));
        return user.IsMaterialReader ? null : Ok(Cost(ProductCostResponse.CodeEnum._403Enum, "无权核算产品成本", null));
    }

    private IActionResult? ResolveAnalysisReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null) return Ok(Analysis(DemandAnalysisResponse.CodeEnum._401Enum, "登录状态无效", null));
        return user.IsMaterialReader ? null : Ok(Analysis(DemandAnalysisResponse.CodeEnum._403Enum, "无权访问需求分析", null));
    }

    private IActionResult? ResolveAnalysisManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null) return Ok(Analysis(DemandAnalysisResponse.CodeEnum._401Enum, "登录状态无效", null));
        return user.IsMaterialManager ? null : Ok(Analysis(DemandAnalysisResponse.CodeEnum._403Enum, "无权维护需求分析", null));
    }

    private IActionResult? ResolveManagerOrForbidden(Func<LossCompensationResponse.CodeEnum, string, List<LossCompensationItem>?, LossCompensationResponse> response)
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null) return Ok(response(LossCompensationResponse.CodeEnum._401Enum, "登录状态无效", null));
        return user.IsMaterialManager ? null : Ok(response(LossCompensationResponse.CodeEnum._403Enum, "无权计算损耗补偿", null));
    }

    private static ProductCostResponse Cost(ProductCostResponse.CodeEnum code, string message, ProductCostResult? data) => new()
    { Code = code, Message = message, Data = data! };
    private static LossCompensationResponse Loss(LossCompensationResponse.CodeEnum code, string message, List<LossCompensationItem>? data) => new()
    { Code = code, Message = message, Data = data! };
    private static DemandAnalysisResponse Analysis(DemandAnalysisResponse.CodeEnum code, string message, DemandAnalysis? data) => new()
    { Code = code, Message = message, Data = data! };
}
