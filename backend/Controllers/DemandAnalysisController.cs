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
    AuthorizationService authorization) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("calculateLossCompensation")]
    public IActionResult CalculateLossCompensation([FromBody] LossCompensationCalculateRequest? request)
    {
        if (RequirePermission(
                PermissionCode.MaterialLossCalculateEnum,
                (code, message) => Loss((LossCompensationResponse.CodeEnum)code, message, null)) is { } forbidden)
        {
            return forbidden;
        }
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
        if (RequirePermission(
                PermissionCode.MaterialCostCalculateEnum,
                (code, message) => Cost((ProductCostResponse.CodeEnum)code, message, null)) is { } forbidden)
        {
            return forbidden;
        }
        if (request is null) return Ok(Cost(ProductCostResponse.CodeEnum._400Enum, "请求体不能为空", null));
        var result = demandAnalysisService.CalculateProductCost(request);
        return Ok(Cost(result.Ok ? ProductCostResponse.CodeEnum._200Enum : (ProductCostResponse.CodeEnum)(int)result.Error,
            result.Ok ? "计算成功" : result.ErrorMessage ?? "计算失败", result.Data));
    }

    private IActionResult? RequirePermission(
        PermissionCode permissionCode,
        Func<int, string, object> responseFactory)
    {
        AuthResult result = authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);
        return result.Ok ? null : Ok(responseFactory(result.Code, result.Message ?? "没有权限访问该接口"));
    }

    private static ProductCostResponse Cost(ProductCostResponse.CodeEnum code, string message, ProductCostResult? data) => new()
    { Code = code, Message = message, Data = data! };
    private static LossCompensationResponse Loss(LossCompensationResponse.CodeEnum code, string message, List<LossCompensationItem>? data) => new()
    { Code = code, Message = message, Data = data! };
}
