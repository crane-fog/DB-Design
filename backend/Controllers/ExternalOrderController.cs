using Backend.Filters;
using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 外部订单接口（C 模块）。数据范围由 own/all 权限决定。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class ExternalOrderController(
    ExternalOrderService externalOrderService,
    AuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listExternalOrder")]
    public IActionResult List(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "customer_id")] long? customerId,
        [FromQuery(Name = "status")] ExternalOrderStatus? status)
    {
        AuthResult auth = authorization.RequireAnyPermission(
            User.GetEmployeeNo(),
            PermissionCode.ExternalOrderViewAllEnum,
            PermissionCode.ExternalOrderViewOwnEnum);
        if (!auth.Ok)
        {
            return Ok(Page((ExternalOrderPageResponse.CodeEnum)auth.Code, auth.Message ?? "无权查看外部订单", null));
        }

        CurrentUser user = auth.User!;
        long? effectiveCustomerId = user.HasPermission(PermissionCode.ExternalOrderViewAllEnum)
            ? customerId
            : user.UserId;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = externalOrderService.List(
            currentPage, size, effectiveCustomerId, ExternalOrderStatusMap.ToDbOrNull(status));

        return Ok(new ExternalOrderPageResponse
        {
            Code = ExternalOrderPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new ExternalOrderPageResponseAllOfData
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records,
            },
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addExternalOrder")]
    public IActionResult Add([FromBody] ExternalOrderCreateRequest? request)
    {
        AuthResult auth = authorization.RequireAnyPermission(
            User.GetEmployeeNo(),
            PermissionCode.ExternalOrderCreateForCustomerEnum,
            PermissionCode.ExternalOrderCreateOwnEnum);
        if (!auth.Ok)
        {
            return Ok(Single((ExternalOrderResponse.CodeEnum)auth.Code, auth.Message ?? "无权提交外部订单", null));
        }

        if (request is null)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        CurrentUser user = auth.User!;
        long customerId;
        if (user.HasPermission(PermissionCode.ExternalOrderCreateForCustomerEnum)
            && request.CustomerId is not null and not 0)
        {
            customerId = request.CustomerId.Value;
        }
        else if (user.HasPermission(PermissionCode.ExternalOrderCreateOwnEnum))
        {
            customerId = user.UserId;
        }
        else
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._400Enum, "代录外部订单需指定客户", null));
        }

        return FromResult(externalOrderService.Create(request, customerId), "提交成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("reviewExternalOrder")]
    [RequireJsonFields("accepted")]
    public IActionResult Review([FromBody] ExternalOrderReviewRequest? request)
    {
        if (RequirePermission(PermissionCode.ExternalOrderReviewEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(externalOrderService.Review(request), request.Accepted ? "已接受" : "已拒绝");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("convertExternalOrderToProductionOrder")]
    public IActionResult Convert([FromBody] ExternalOrderConvertRequest? request)
    {
        AuthResult auth = authorization.RequirePermission(
            User.GetEmployeeNo(),
            PermissionCode.ExternalOrderConvertEnum);
        if (!auth.Ok)
        {
            return Ok(ConvertResp((ExternalOrderConvertResponse.CodeEnum)auth.Code, auth.Message ?? "无权转换外部订单", null));
        }

        if (request is null)
        {
            return Ok(ConvertResp(ExternalOrderConvertResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        var outcome = externalOrderService.ConvertToProductionOrders(request);
        if (outcome.Ok)
        {
            return Ok(ConvertResp(ExternalOrderConvertResponse.CodeEnum._200Enum, "转换成功", outcome.Result));
        }

        var code = (ExternalOrderConvertResponse.CodeEnum)outcome.ErrorCode;
        return Ok(ConvertResp(code, outcome.ErrorMessage ?? "转换失败", null));
    }

    private IActionResult? RequirePermission(PermissionCode permissionCode)
    {
        AuthResult result = authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);
        return result.Ok
            ? null
            : Ok(Single((ExternalOrderResponse.CodeEnum)result.Code, result.Message ?? "无权操作外部订单", null));
    }

    private IActionResult FromResult(ExternalOrderResult result, string successMessage)
    {
        if (result.Ok)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._200Enum, successMessage, result.Order));
        }

        var code = (ExternalOrderResponse.CodeEnum)result.ErrorCode;
        return Ok(Single(code, result.ErrorMessage ?? "操作失败", null));
    }

    private static ExternalOrderResponse Single(
        ExternalOrderResponse.CodeEnum code,
        string message,
        ExternalOrder? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static ExternalOrderPageResponse Page(
        ExternalOrderPageResponse.CodeEnum code,
        string message,
        ExternalOrderPageResponseAllOfData? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static ExternalOrderConvertResponse ConvertResp(
        ExternalOrderConvertResponse.CodeEnum code,
        string message,
        ExternalOrderConvertResult? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };
}
