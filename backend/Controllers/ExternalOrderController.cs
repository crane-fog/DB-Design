using Backend.Filters;
using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 外部订单接口（C 模块）。外部客户只能查询/提交自己的订单；审核和转换仅管理员可用。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class ExternalOrderController(
    ExternalOrderService externalOrderService,
    UserContextService userContext) : ControllerBase
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
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Page(ExternalOrderPageResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        // 数据自限：外部客户强制只看自己的订单（忽略传入 customer_id）；
        // 管理员可查看全部或按 customer_id 过滤；其余内部角色无权查看外部订单。
        long? effectiveCustomerId;
        if (user.IsExternalCustomer)
        {
            effectiveCustomerId = user.UserId;
        }
        else if (user.IsProductionManager)
        {
            effectiveCustomerId = customerId;
        }
        else
        {
            return Ok(Page(ExternalOrderPageResponse.CodeEnum._403Enum, "无权查看外部订单", null));
        }

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
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (request is null)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        // 外部客户只能为自己提交；管理员可代录并显式指定 customer_id。
        long customerId;
        if (user.IsExternalCustomer)
        {
            customerId = user.UserId;
        }
        else if (user.IsProductionManager)
        {
            if (request.CustomerId is null or 0)
            {
                return Ok(Single(ExternalOrderResponse.CodeEnum._400Enum, "代录外部订单需指定客户", null));
            }

            customerId = request.CustomerId.Value;
        }
        else
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._403Enum, "无权提交外部订单", null));
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
        if (ResolveManagerOrForbidden() is { } forbidden)
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
        // 鉴权内联：必须返回 ExternalOrderConvertResponse 而非 ExternalOrderResponse，
        // 否则 OpenAPI 契约中 convert 接口的响应体类型不匹配。
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(ConvertResp(ExternalOrderConvertResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsProductionManager)
        {
            return Ok(ConvertResp(ExternalOrderConvertResponse.CodeEnum._403Enum, "无权操作外部订单", null));
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

    /// <summary>校验当前用户为生产/系统管理员；否则返回相应响应对象。</summary>
    private IActionResult? ResolveManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsProductionManager)
        {
            return Ok(Single(ExternalOrderResponse.CodeEnum._403Enum, "无权操作外部订单", null));
        }

        return null;
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
