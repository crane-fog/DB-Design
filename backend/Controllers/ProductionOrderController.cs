using Backend.Filters;
using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 生产订单接口（C 模块）。各接口分别检查稳定权限码。
/// HTTP 固定 200，业务状态通过响应体 code 表达。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class ProductionOrderController(
    ProductionOrderService orderService,
    ProductionCompletionService completionService,
    AuthorizationService authorization) : ControllerBase
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
        if (RequirePermission(PermissionCode.ProductionOrderViewEnum) is { } forbidden)
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
        if (RequirePermission(PermissionCode.ProductionOrderViewEnum) is { } forbidden)
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
    [OperationAudit("生产订单", "新增生产订单")]
    public IActionResult Add([FromBody] ProductionOrderCreateRequest? request)
    {
        if (RequirePermission(PermissionCode.ProductionOrderCreateEnum) is { } forbidden)
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
    [OperationAudit("生产订单", "修改生产订单", OperationAuditSnapshotKind.ProductionOrder)]
    public IActionResult Update([FromBody] ProductionOrderUpdateRequest? request)
    {
        if (RequirePermission(PermissionCode.ProductionOrderUpdateEnum) is { } forbidden)
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
    [OperationAudit("生产订单", "审核生产订单", OperationAuditSnapshotKind.ProductionOrder)]
    [RequireJsonFields("approved")]
    public IActionResult Approve([FromBody] ProductionOrderApproveRequest? request)
    {
        if (RequirePermission(PermissionCode.ProductionOrderApproveEnum) is { } forbidden)
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
    [OperationAudit("生产订单", "开始生产订单", OperationAuditSnapshotKind.ProductionOrder)]
    public IActionResult Start([FromBody] ProductionOrderActionRequest? request)
    {
        if (RequirePermission(PermissionCode.ProductionOrderStartEnum) is { } forbidden)
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
    [OperationAudit("生产订单", "完成生产订单", OperationAuditSnapshotKind.ProductionOrder)]
    public IActionResult Finish([FromBody] ProductionOrderFinishRequest? request)
    {
        if (RequirePermission(PermissionCode.ProductionOrderFinishEnum) is { } forbidden)
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
    [Route("reportProductionCompletion")]
    [OperationAudit("生产订单", "完工报工", OperationAuditSnapshotKind.ProductionOrder)]
    [RequireJsonFields("order_id", "finish_qty", "qualified_qty", "batch_no")]
    public IActionResult ReportCompletion([FromBody] ProductionCompletionReportRequest? request)
    {
        AuthResult auth = authorization.RequirePermission(
            User.GetEmployeeNo(),
            PermissionCode.ProductionOrderFinishEnum);
        if (!auth.Ok)
        {
            return Ok(ReportDetail(
                (ProductionCompletionReportResponse.CodeEnum)auth.Code,
                auth.Message ?? "无权进行完工报工",
                null));
        }

        if (request is null)
        {
            return Ok(ReportDetail(
                ProductionCompletionReportResponse.CodeEnum._400Enum,
                "请求体不能为空",
                null));
        }

        ProductionCompletionReportOutcome result = completionService.Report(request, auth.User!);
        return result.Ok
            ? Ok(ReportDetail(
                ProductionCompletionReportResponse.CodeEnum._200Enum,
                result.Data!.OrderCompleted ? "报工成功，生产订单已完工" : "报工成功",
                result.Data))
            : Ok(ReportDetail(
                (ProductionCompletionReportResponse.CodeEnum)result.ErrorCode,
                result.ErrorMessage ?? "完工报工失败",
                null));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("cancelProductionOrder")]
    [OperationAudit("生产订单", "取消生产订单", OperationAuditSnapshotKind.ProductionOrder)]
    public IActionResult Cancel([FromBody] ProductionOrderActionRequest? request)
    {
        if (RequirePermission(PermissionCode.ProductionOrderCancelEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Detail(ProductionOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(orderService.Cancel(request), "已取消");
    }

    private IActionResult? RequirePermission(PermissionCode permissionCode)
    {
        AuthResult result = authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);
        return result.Ok
            ? null
            : Ok(Detail((ProductionOrderResponse.CodeEnum)result.Code, result.Message ?? "无权访问生产订单", null));
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

    private static ProductionCompletionReportResponse ReportDetail(
        ProductionCompletionReportResponse.CodeEnum code,
        string message,
        ProductionCompletionReportResult? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };
}
