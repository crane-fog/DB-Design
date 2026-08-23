using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 采购管理接口（B 模块）。所有接口要求登录，权限为采购员/采购主管/系统管理员。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class PurchaseController(
    PurchaseService purchaseService,
    UserContextService userContext) : ControllerBase
{
    // ═══════════════════════════════════════════════════════════════
    //  GET /api/listPurchaseOrder
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    [Produces("application/json")]
    [Route("listPurchaseOrder")]
    public IActionResult List(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "supplier_id")] long? supplierId,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "status")] PurchaseOrderStatus? status,
        [FromQuery(Name = "order_date_start")] DateOnly? orderDateStart,
        [FromQuery(Name = "order_date_end")] DateOnly? orderDateEnd,
        [FromQuery(Name = "expected_date_start")] DateOnly? expectedDateStart,
        [FromQuery(Name = "expected_date_end")] DateOnly? expectedDateEnd,
        [FromQuery(Name = "buyer_id")] long? buyerId)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = purchaseService.List(
            currentPage, size, supplierId, materialId,
            PurchaseOrderStatusMap.ToDbOrNull(status),
            orderDateStart, orderDateEnd, expectedDateStart, expectedDateEnd, buyerId);

        return Ok(new PurchaseOrderPageResponse
        {
            Code = PurchaseOrderPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new PurchaseOrderPageData
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records,
            },
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  GET /api/getPurchaseOrder
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    [Produces("application/json")]
    [Route("getPurchaseOrder")]
    public IActionResult Get([FromQuery(Name = "order_id")] long orderId)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        var order = purchaseService.Get(orderId);
        return order is null
            ? Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._404Enum, "采购订单不存在", null))
            : Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._200Enum, "查询成功", order));
    }

    // ═══════════════════════════════════════════════════════════════
    //  POST /api/addPurchaseOrder
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addPurchaseOrder")]
    public IActionResult Add([FromBody] PurchaseOrderCreateRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        if (request is null)
            return Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));

        return FromResult(purchaseService.Create(request), "创建成功");
    }

    // ═══════════════════════════════════════════════════════════════
    //  POST /api/createPurchaseOrderDraftFromShortage
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("createPurchaseOrderDraftFromShortage")]
    public IActionResult CreateDraftsFromShortage([FromBody] PurchaseDraftFromShortageRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        if (request is null || request.Items is null || request.Items.Count == 0)
            return Ok(new PurchaseDraftFromShortageResponse
            {
                Code = PurchaseDraftFromShortageResponse.CodeEnum._400Enum,
                Message = "请求体不能为空且需包含至少一个物料项",
                Data = null!,
            });

        var result = purchaseService.CreateDraftsFromShortage(request);
        var code = (PurchaseDraftFromShortageResponse.CodeEnum)result.ErrorCode;

        return Ok(new PurchaseDraftFromShortageResponse
        {
            Code = code,
            Message = result.Ok ? "生成完成" : result.ErrorMessage ?? "生成失败",
            Data = result.Ok
                ? new PurchaseDraftFromShortageResponseAllOfData
                {
                    CreatedCount = result.CreatedCount,
                    Records = result.Records,
                    UnassignedItems = result.UnassignedItems,
                }
                : null!,
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  POST /api/submitPurchaseOrder
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("submitPurchaseOrder")]
    public IActionResult Submit([FromBody] PurchaseOrderActionRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        if (request is null)
            return Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));

        return FromResult(purchaseService.Submit(request.OrderId, request.OperatorId), "已提交");
    }

    // ═══════════════════════════════════════════════════════════════
    //  POST /api/cancelPurchaseOrder
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("cancelPurchaseOrder")]
    public IActionResult Cancel([FromBody] PurchaseOrderActionRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        if (request is null)
            return Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._400Enum, "请求体不能为空", null));

        return FromResult(purchaseService.Cancel(request.OrderId, request.OperatorId), "已取消");
    }

    // ═══════════════════════════════════════════════════════════════
    //  POST /api/addPurchaseReceipt
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addPurchaseReceipt")]
    public IActionResult AddReceipt([FromBody] PurchaseReceiptCreateRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        if (request is null)
            return Ok(ReceiptSingle(PurchaseReceiptResponse.CodeEnum._400Enum, "请求体不能为空", null));

        var result = purchaseService.AddReceipt(request);
        var code = result.Ok
            ? PurchaseReceiptResponse.CodeEnum._200Enum
            : (PurchaseReceiptResponse.CodeEnum)result.ErrorCode;

        return Ok(new PurchaseReceiptResponse
        {
            Code = code,
            Message = result.Ok ? "收货完成" : result.ErrorMessage ?? "操作失败",
            Data = result.Receipt!,
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  GET /api/listPurchaseReceipt
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    [Produces("application/json")]
    [Route("listPurchaseReceipt")]
    public IActionResult ListReceipts(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "order_id")] long? orderId,
        [FromQuery(Name = "material_id")] long? materialId)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = purchaseService.ListReceipts(currentPage, size, orderId, materialId);

        return Ok(new PurchaseReceiptPageResponse
        {
            Code = PurchaseReceiptPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new PurchaseReceiptPageData
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records,
            },
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  逾期提醒
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("generatePurchaseOverdueReminder")]
    public IActionResult GenerateReminders([FromBody] PurchaseOverdueReminderGenerateRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        long? orderId = request?.OrderId;
        var (count, records) = purchaseService.GenerateReminders(orderId);

        return Ok(new PurchaseOverdueReminderGenerateResponse
        {
            Code = PurchaseOverdueReminderGenerateResponse.CodeEnum._200Enum,
            Message = "逾期提醒生成完成",
            Data = new PurchaseOverdueReminderGenerateResponseAllOfData
            {
                GeneratedCount = count,
                Records = records,
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listPurchaseOverdueReminder")]
    public IActionResult ListReminders(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "order_id")] long? orderId,
        [FromQuery(Name = "status")] PurchaseOverdueReminderStatus? status)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = purchaseService.ListReminders(
            currentPage, size, orderId, PurchaseOverdueReminderStatusMap.ToDbOrNull(status));

        return Ok(new PurchaseOverdueReminderPageResponse
        {
            Code = PurchaseOverdueReminderPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new PurchaseOverdueReminderPageData
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
    [Route("handlePurchaseOverdueReminder")]
    public IActionResult HandleReminder([FromBody] PurchaseOverdueReminderHandleRequest? request)
    {
        if (ResolvePurchaserOrForbidden() is { } forbidden) return forbidden;

        if (request is null)
            return Ok(ReminderSingle(PurchaseOverdueReminderResponse.CodeEnum._400Enum, "请求体不能为空", null));

        var statusStr = request.Status switch
        {
            PurchaseOverdueReminderHandleRequest.StatusEnum.UrgedEnum => "urged",
            PurchaseOverdueReminderHandleRequest.StatusEnum.ReceivedEnum => "received",
            _ => "",
        };

        var result = purchaseService.HandleReminder(request.ReminderId, statusStr, request.Remark);
        if (!result.Ok)
        {
            var code = (PurchaseOverdueReminderResponse.CodeEnum)result.ErrorCode;
            return Ok(ReminderSingle(code, result.ErrorMessage ?? "操作失败", null));
        }

        return Ok(ReminderSingle(PurchaseOverdueReminderResponse.CodeEnum._200Enum, "处理成功", result.Reminder));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Authorization helpers
    // ═══════════════════════════════════════════════════════════════

    private IActionResult? ResolvePurchaserOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
            return Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._401Enum, "登录状态无效", null));

        if (!user.IsPurchaser)
            return Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._403Enum, "无权访问采购管理", null));

        return null;
    }

    private IActionResult FromResult(PurchaseResult result, string successMessage)
    {
        if (result.Ok)
            return Ok(OrderSingle(PurchaseOrderResponse.CodeEnum._200Enum, successMessage, result.Order));

        var code = (PurchaseOrderResponse.CodeEnum)result.ErrorCode;
        return Ok(OrderSingle(code, result.ErrorMessage ?? "操作失败", null));
    }

    private static PurchaseOrderResponse OrderSingle(
        PurchaseOrderResponse.CodeEnum code, string message, PurchaseOrder? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static PurchaseReceiptResponse ReceiptSingle(
        PurchaseReceiptResponse.CodeEnum code, string message, PurchaseReceipt? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static PurchaseOverdueReminderResponse ReminderSingle(
        PurchaseOverdueReminderResponse.CodeEnum code, string message, PurchaseOverdueReminder? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };
}
