using Backend.Services;
using Backend.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 库存管理接口（B 模块）。各接口分别检查稳定权限码。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class InventoryController(
    InventoryService inventoryService,
    AuthorizationService authorization) : ControllerBase
{
    // ═══════════════════════════════════════════════════════════════
    //  GET /api/getMaterialStockData
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    [Produces("application/json")]
    [Route("getMaterialStockData")]
    public IActionResult GetStockData([FromQuery(Name = "material_id")] long materialId)
    {
        if (RequirePermission(PermissionCode.InventoryStockViewEnum) is { } forbidden) return forbidden;

        var stock = inventoryService.GetStockData(materialId);
        return stock is null
            ? Ok(new MaterialStockResponse
            {
                Code = MaterialStockResponse.CodeEnum._404Enum,
                Message = "物料库存数据不存在",
                Data = null!,
            })
            : Ok(new MaterialStockResponse
            {
                Code = MaterialStockResponse.CodeEnum._200Enum,
                Message = "查询成功",
                Data = stock,
            });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listMaterialStockData")]
    public IActionResult ListStockData(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "material_name")] string? materialName,
        [FromQuery(Name = "material_type")] string? materialType,
        [FromQuery(Name = "status")] string? status)
    {
        if (RequirePermission(PermissionCode.InventoryStockViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = inventoryService.ListStockData(
            currentPage,
            size,
            materialId,
            materialName,
            materialType,
            status);
        return Ok(new MaterialStockPageResponse
        {
            Code = MaterialStockPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new MaterialStockPageData
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records,
            },
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Inventory Alert
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    [Produces("application/json")]
    [Route("listInventoryAlert")]
    public IActionResult ListAlerts(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "status")] InventoryAlertStatus? status,
        [FromQuery(Name = "start_time")] DateTime? startTime,
        [FromQuery(Name = "end_time")] DateTime? endTime)
    {
        if (RequirePermission(PermissionCode.InventoryAlertViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = inventoryService.ListAlerts(
            currentPage, size, materialId,
            InventoryAlertStatusMap.ToDbOrNull(status), startTime, endTime);

        return Ok(new InventoryAlertPageResponse
        {
            Code = InventoryAlertPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new InventoryAlertPageData
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
    [Route("getInventoryAlert")]
    public IActionResult GetAlert([FromQuery(Name = "alert_id")] long alertId)
    {
        if (RequirePermission(PermissionCode.InventoryAlertViewEnum) is { } forbidden) return forbidden;

        var alert = inventoryService.GetAlert(alertId);
        return alert is null
            ? Ok(AlertSingle(InventoryAlertResponse.CodeEnum._404Enum, "库存预警不存在", null))
            : Ok(AlertSingle(InventoryAlertResponse.CodeEnum._200Enum, "查询成功", alert));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("generateInventoryAlert")]
    public IActionResult GenerateAlerts([FromBody] InventoryAlertGenerateRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryAlertGenerateEnum) is { } forbidden) return forbidden;

        long? materialId = request?.MaterialId;

        var (generated, skipped, records) = inventoryService.GenerateAlerts(materialId);

        return Ok(new InventoryAlertGenerateResponse
        {
            Code = InventoryAlertGenerateResponse.CodeEnum._200Enum,
            Message = "库存预警生成完成",
            Data = new InventoryAlertGenerateResponseAllOfData
            {
                GeneratedCount = generated,
                SkippedPendingCount = skipped,
                Records = records,
            },
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("handleInventoryAlert")]
    public IActionResult HandleAlert([FromBody] InventoryAlertHandleRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryAlertHandleEnum) is { } forbidden) return forbidden;

        if (request is null)
            return Ok(AlertSingle(InventoryAlertResponse.CodeEnum._400Enum, "请求体不能为空", null));

        var statusStr = request.Status switch
        {
            InventoryAlertHandleRequest.StatusEnum.HandledEnum => "handled",
            InventoryAlertHandleRequest.StatusEnum.IgnoredEnum => "ignored",
            _ => "",
        };

        var result = inventoryService.HandleAlert(request.AlertId, statusStr, request.HandlerId);
        if (!result.Ok)
        {
            var code = (InventoryAlertResponse.CodeEnum)result.ErrorCode;
            return Ok(AlertSingle(code, result.ErrorMessage ?? "操作失败", null));
        }

        return Ok(AlertSingle(InventoryAlertResponse.CodeEnum._200Enum, "处理成功", result.Alert));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Stock Lock
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("lockMaterialStock")]
    public IActionResult LockStock([FromBody] MaterialStockLockRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryLockCreateEnum) is { } forbidden) return forbidden;

        if (request is null || request.Items is null || request.Items.Count == 0)
            return Ok(new MaterialStockLockResponse
            {
                Code = MaterialStockLockResponse.CodeEnum._400Enum,
                Message = "请求体不能为空且需包含至少一个物料项",
                Data = null!,
            });

        var result = inventoryService.LockStock(request.OrderId, request.OperatorId, request.Items);
        var code = result.Ok
            ? MaterialStockLockResponse.CodeEnum._200Enum
            : (MaterialStockLockResponse.CodeEnum)result.ErrorCode;

        return Ok(new MaterialStockLockResponse
        {
            Code = code,
            Message = result.Ok ? "锁定完成" : result.ErrorMessage ?? "操作失败",
            Data = result.Data!,
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("releaseMaterialStock")]
    public IActionResult ReleaseStock([FromBody] MaterialStockReleaseRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryLockReleaseEnum) is { } forbidden) return forbidden;

        if (request is null)
            return Ok(LockSingle(StockLockRecordResponse.CodeEnum._400Enum, "请求体不能为空", null));

        var result = inventoryService.ReleaseStock(request.LockId, request.OperatorId);
        if (!result.Ok)
        {
            var code = (StockLockRecordResponse.CodeEnum)result.ErrorCode;
            return Ok(LockSingle(code, result.ErrorMessage ?? "操作失败", null));
        }

        return Ok(LockSingle(StockLockRecordResponse.CodeEnum._200Enum, "释放成功", result.Record));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listMaterialStockLock")]
    public IActionResult ListLocks(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "order_id")] long? orderId,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "status")] StockLockStatus? status)
    {
        if (RequirePermission(PermissionCode.InventoryLockViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = inventoryService.ListLocks(
            currentPage, size, orderId, materialId, StockLockStatusMap.ToDbOrNull(status));

        return Ok(new StockLockRecordPageResponse
        {
            Code = StockLockRecordPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new StockLockRecordPageData
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records,
            },
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Obsolete Material Detection
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("detectObsoleteMaterial")]
    public IActionResult DetectObsolete([FromBody] ObsoleteMaterialDetectRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryObsoleteDetectEnum) is { } forbidden) return forbidden;

        if (request is null || request.IdleDaysThreshold < 1)
            return Ok(new ObsoleteMaterialDetectResponse
            {
                Code = ObsoleteMaterialDetectResponse.CodeEnum._400Enum,
                Message = "闲置天数阈值必须 >= 1",
                Data = null!,
            });

        var (count, records) = inventoryService.DetectObsolete(
            request.IdleDaysThreshold, request.MaterialId);

        return Ok(new ObsoleteMaterialDetectResponse
        {
            Code = ObsoleteMaterialDetectResponse.CodeEnum._200Enum,
            Message = "废弃物料检测完成",
            Data = new ObsoleteMaterialDetectResponseAllOfData
            {
                DetectedCount = count,
                Records = records,
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listObsoleteMaterialDetection")]
    public IActionResult ListDetections(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "status")] ObsoleteMaterialStatus? status,
        [FromQuery(Name = "detect_time_start")] DateTime? detectTimeStart,
        [FromQuery(Name = "detect_time_end")] DateTime? detectTimeEnd)
    {
        if (RequirePermission(PermissionCode.InventoryObsoleteViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = inventoryService.ListDetections(
            currentPage, size, materialId,
            ObsoleteMaterialStatusMap.ToDbOrNull(status), detectTimeStart, detectTimeEnd);

        return Ok(new ObsoleteMaterialPageResponse
        {
            Code = ObsoleteMaterialPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new ObsoleteMaterialPageData
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
    [Route("getObsoleteMaterialDetection")]
    public IActionResult GetDetection([FromQuery(Name = "detection_id")] long detectionId)
    {
        if (RequirePermission(PermissionCode.InventoryObsoleteViewEnum) is { } forbidden) return forbidden;

        var detection = inventoryService.GetDetection(detectionId);
        return detection is null
            ? Ok(ObsoleteSingle(ObsoleteMaterialResponse.CodeEnum._404Enum, "废弃物料检测记录不存在", null))
            : Ok(ObsoleteSingle(ObsoleteMaterialResponse.CodeEnum._200Enum, "查询成功", detection));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("handleObsoleteMaterialDetection")]
    public IActionResult HandleDetection([FromBody] ObsoleteMaterialHandleRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryObsoleteHandleEnum) is { } forbidden) return forbidden;

        if (request is null)
            return Ok(ObsoleteSingle(ObsoleteMaterialResponse.CodeEnum._400Enum, "请求体不能为空", null));

        var statusStr = request.Status switch
        {
            ObsoleteMaterialHandleRequest.StatusEnum.HandledEnum => "handled",
            ObsoleteMaterialHandleRequest.StatusEnum.IgnoredEnum => "ignored",
            _ => "",
        };

        var result = inventoryService.HandleDetection(request.DetectionId, statusStr, request.HandlerId);
        if (!result.Ok)
        {
            var code = (ObsoleteMaterialResponse.CodeEnum)result.ErrorCode;
            return Ok(ObsoleteSingle(code, result.ErrorMessage ?? "操作失败", null));
        }

        return Ok(ObsoleteSingle(ObsoleteMaterialResponse.CodeEnum._200Enum, "处理成功", result.Detection));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Completion Inbound
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addCompletionInbound")]
    public IActionResult AddInbound([FromBody] CompletionInboundCreateRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryCompletionCreateEnum) is { } forbidden) return forbidden;

        if (request is null)
            return Ok(InboundSingle(CompletionInboundResponse.CodeEnum._400Enum, "请求体不能为空", null));

        var result = inventoryService.AddInbound(request);
        var code = result.Ok
            ? CompletionInboundResponse.CodeEnum._200Enum
            : (CompletionInboundResponse.CodeEnum)result.ErrorCode;

        return Ok(new CompletionInboundResponse
        {
            Code = code,
            Message = result.Ok ? "入库成功" : result.ErrorMessage ?? "操作失败",
            Data = result.Order!,
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listCompletionInbound")]
    public IActionResult ListInbound(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "order_id")] long? orderId,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "inbound_time_start")] DateTime? inboundTimeStart,
        [FromQuery(Name = "inbound_time_end")] DateTime? inboundTimeEnd)
    {
        if (RequirePermission(PermissionCode.InventoryCompletionViewEnum) is { } forbidden) return forbidden;

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = inventoryService.ListInbound(
            currentPage, size, orderId, materialId, inboundTimeStart, inboundTimeEnd);

        return Ok(new CompletionInboundPageResponse
        {
            Code = CompletionInboundPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new CompletionInboundPageData
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
    [Route("getCompletionInbound")]
    public IActionResult GetInbound([FromQuery(Name = "inbound_id")] long inboundId)
    {
        if (RequirePermission(PermissionCode.InventoryCompletionViewEnum) is { } forbidden) return forbidden;

        var inbound = inventoryService.GetInbound(inboundId);
        return inbound is null
            ? Ok(InboundSingle(CompletionInboundResponse.CodeEnum._404Enum, "成品入库记录不存在", null))
            : Ok(InboundSingle(CompletionInboundResponse.CodeEnum._200Enum, "查询成功", inbound));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Calculate Material Shortage
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("calculateMaterialShortage")]
    public IActionResult CalculateShortage([FromBody] MaterialShortageCalculateRequest? request)
    {
        if (RequirePermission(PermissionCode.InventoryShortageCalculateEnum) is { } forbidden) return forbidden;

        if (request is null || request.Items is null || request.Items.Count == 0)
            return Ok(new MaterialShortageCalculateResponse
            {
                Code = MaterialShortageCalculateResponse.CodeEnum._400Enum,
                Message = "请求体不能为空且需包含至少一个物料项",
                Data = null!,
            });

        var result = inventoryService.CalculateShortage(request);
        var code = result.Ok
            ? MaterialShortageCalculateResponse.CodeEnum._200Enum
            : (MaterialShortageCalculateResponse.CodeEnum)result.ErrorCode;

        return Ok(new MaterialShortageCalculateResponse
        {
            Code = code,
            Message = result.Ok ? "计算完成" : result.ErrorMessage ?? "计算失败",
            Data = result.Ok
                ? new MaterialShortageCalculateResponseAllOfData
                {
                    CalculationTime = result.CalculationTime,
                    Records = result.Records!,
                }
                : null!,
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Authorization helpers
    // ═══════════════════════════════════════════════════════════════

    private IActionResult? RequirePermission(PermissionCode permissionCode)
    {
        AuthResult result = authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);
        return result.Ok
            ? null
            : Ok(new ApiResponse
            {
                Code = (ApiResponse.CodeEnum)result.Code,
                Message = result.Message ?? "无权访问库存管理",
                Data = null!,
            });
    }

    private static InventoryAlertResponse AlertSingle(
        InventoryAlertResponse.CodeEnum code, string message, InventoryAlertEvent? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static StockLockRecordResponse LockSingle(
        StockLockRecordResponse.CodeEnum code, string message, StockLockRecord? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static ObsoleteMaterialResponse ObsoleteSingle(
        ObsoleteMaterialResponse.CodeEnum code, string message, ObsoleteMaterialDetection? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static CompletionInboundResponse InboundSingle(
        CompletionInboundResponse.CodeEnum code, string message, CompletionInboundOrder? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };
}
