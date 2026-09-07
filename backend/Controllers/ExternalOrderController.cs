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
        [FromQuery(Name = "customer_name")] string? customerName,
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
        bool canViewAll = user.HasPermission(PermissionCode.ExternalOrderViewAllEnum);
        long? effectiveCustomerId = canViewAll ? null : user.UserId;
        string? effectiveCustomerName = canViewAll ? customerName : null;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = externalOrderService.List(
            currentPage,
            size,
            effectiveCustomerId,
            effectiveCustomerName,
            ExternalOrderStatusMap.ToDbOrNull(status));

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

    [HttpGet]
    [Produces("application/json")]
    [Route("listExternalOrderFormOptions")]
    public IActionResult ListFormOptions()
    {
        AuthResult auth = authorization.RequireAnyPermission(
            User.GetEmployeeNo(),
            PermissionCode.ExternalOrderCreateForCustomerEnum,
            PermissionCode.ExternalOrderCreateOwnEnum);
        if (!auth.Ok)
        {
            return Ok(FormOptionsResp(
                (ExternalOrderFormOptionsResponse.CodeEnum)auth.Code,
                auth.Message ?? "无权查询外部订单表单选项",
                null));
        }

        bool includeCustomers = auth.User!.HasPermission(
            PermissionCode.ExternalOrderCreateForCustomerEnum);
        ExternalOrderFormOptions options = externalOrderService.GetFormOptions(includeCustomers);
        return Ok(FormOptionsResp(
            ExternalOrderFormOptionsResponse.CodeEnum._200Enum,
            "查询成功",
            options));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addExternalOrder")]
    [OperationAudit("外部订单", "新增外部订单")]
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
        bool canCreateForCustomer = user.HasPermission(
            PermissionCode.ExternalOrderCreateForCustomerEnum);
        long customerId;
        if (canCreateForCustomer)
        {
            if (request.CustomerId is null or <= 0)
            {
                return Ok(Single(
                    ExternalOrderResponse.CodeEnum._400Enum,
                    "代录外部订单必须选择客户",
                    null));
            }

            customerId = request.CustomerId.Value;
        }
        else
        {
            if (request.CustomerId is not null and not 0)
            {
                return Ok(Single(
                    ExternalOrderResponse.CodeEnum._400Enum,
                    "外部客户只能为本人提交订单",
                    null));
            }

            customerId = user.UserId;
        }

        return FromResult(
            externalOrderService.Create(request, customerId, canCreateForCustomer),
            "提交成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("reviewExternalOrder")]
    [OperationAudit("外部订单", "审核外部订单", OperationAuditSnapshotKind.ExternalOrder)]
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
    [OperationAudit("外部订单", "转为生产订单", OperationAuditSnapshotKind.ExternalOrder)]
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

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deliverExternalOrder")]
    [OperationAudit("外部订单", "整单交货", OperationAuditSnapshotKind.ExternalOrder)]
    [RequireJsonFields("ext_order_id")]
    public IActionResult Deliver([FromBody] ExternalOrderDeliveryRequest? request)
    {
        AuthResult auth = authorization.RequirePermission(
            User.GetEmployeeNo(),
            PermissionCode.ExternalOrderConvertEnum);
        if (!auth.Ok)
        {
            return Ok(DeliveryResp(
                (ExternalOrderDeliveryResponse.CodeEnum)auth.Code,
                auth.Message ?? "无权交付外部订单",
                null));
        }

        if (request is null)
        {
            return Ok(DeliveryResp(
                ExternalOrderDeliveryResponse.CodeEnum._400Enum,
                "请求体不能为空",
                null));
        }

        ExternalOrderDeliveryOutcome outcome = externalOrderService.Deliver(
            request.ExtOrderId,
            auth.User!.UserId);
        if (outcome.Ok)
        {
            return Ok(DeliveryResp(
                ExternalOrderDeliveryResponse.CodeEnum._200Enum,
                "交货成功",
                outcome.Result));
        }

        var code = (ExternalOrderDeliveryResponse.CodeEnum)outcome.ErrorCode;
        return Ok(DeliveryResp(code, outcome.ErrorMessage ?? "交货失败", null));
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

    private static ExternalOrderFormOptionsResponse FormOptionsResp(
        ExternalOrderFormOptionsResponse.CodeEnum code,
        string message,
        ExternalOrderFormOptions? data) => new()
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

    private static ExternalOrderDeliveryResponse DeliveryResp(
        ExternalOrderDeliveryResponse.CodeEnum code,
        string message,
        ExternalOrderDeliveryResult? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };
}
