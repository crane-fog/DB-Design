using Backend.Filters;
using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 生产订单接口（C 模块）。所有接口要求登录；外部客户不可访问生产订单。
/// HTTP 固定 200，业务状态通过响应体 code 表达。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class ProductionOrderController(
    ProductionOrderService orderService,
    UserContextService userContext) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listProductionOrder")]
    public IActionResult List(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "status")] ProductionOrderStatus? status,
        [FromQuery(Name = "plan_end_start")] DateOnly? planEndStart,
        [FromQuery(Name = "plan_end_end")] DateOnly? planEndEnd)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = orderService.List(
            currentPage, size, materialId, ProductionStatusMap.ToDbOrNull(status), planEndStart, planEndEnd);

        return Ok(new ProductionOrderPageResponse
        {
            Code = ProductionOrderPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new ProductionOrderPageResponseAllOfData
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
    [Route("getProductionOrder")]
    public IActionResult Get([FromQuery(Name = "order_id")] long orderId)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var order = orderService.Get(orderId);
        return order is null
            ? Ok(Detail(ProductionOrderResponse.CodeEnum._404Enum, "生产订单不存在", null))
            : Ok(Detail(ProductionOrderResponse.CodeEnum._200Enum, "查询成功", order));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addProductionOrder")]
    public IActionResult Add([FromBody] ProductionOrderCreateRequest? request)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Create(request), "创建成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateProductionOrder")]
    public IActionResult Update([FromBody] ProductionOrderUpdateRequest? request)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Update(request), "修改成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("approveProductionOrder")]
    [RequireJsonFields("approved")]
    public IActionResult Approve([FromBody] ProductionOrderApproveRequest? request)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Approve(request), request.Approved ? "审核通过" : "已拒绝");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("startProductionOrder")]
    public IActionResult Start([FromBody] ProductionOrderActionRequest? request)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Start(request), "已开工");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("finishProductionOrder")]
    public IActionResult Finish([FromBody] ProductionOrderFinishRequest? request)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Finish(request), "已完工");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("cancelProductionOrder")]
    public IActionResult Cancel([FromBody] ProductionOrderActionRequest? request)
    {
        if (ResolveManagerOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Cancel(request), "已取消");
    }

    /// <summary>校验当前用户为生产/系统管理员；否则返回 403 响应对象。</summary>
    private IActionResult? ResolveManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsProductionManager)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._403Enum, "无权访问生产订单", null));
        }

        return null;
    }

    private IActionResult FromResult(ProductionOrderResult result, string successMessage)
    {
        if (result.Ok)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._200Enum, successMessage, result.Order));
        }

        var code = (ProductionOrderResponse.CodeEnum)result.ErrorCode;
        return Ok(Detail(code, result.ErrorMessage ?? "操作失败", null));
    }

    private static ProductionOrderResponse Detail(
        ProductionOrderResponse.CodeEnum code,
        string message,
        ProductionOrderDetail? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };
}
