using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// D 模块：生产线、产能配置、生产日历、产能估算、产能检测、故障与产线状态。
/// HTTP 固定返回 200，业务状态通过响应体 code 表达。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public sealed class ProductionResourceController(
    IProductionLineService productionLineService,
    ICapacityService capacityService,
    UserContextService userContext) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listProductionLine")]
    public IActionResult ListProductionLine(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "type_id")] long? typeId,
        [FromQuery(Name = "status")] ProductionLineRunStatus? status)
    {
        if (RequireProductionManager(ProductionLinePageError) is { } error)
        {
            return error;
        }

        (int currentPage, int size) = Paging.Normalize(page, pageSize);
        return ProductionLinePage(
            productionLineService.ListLines(currentPage, size, typeId, status));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listProductionLineType")]
    public IActionResult ListProductionLineType(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "type_name")] string? typeName)
    {
        if (RequireProductionManager(LineTypePageError) is { } error)
        {
            return error;
        }

        (int currentPage, int size) = Paging.Normalize(page, pageSize);
        return LineTypePage(
            productionLineService.ListLineTypes(currentPage, size, typeName));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("saveProductionLineType")]
    public IActionResult SaveProductionLineType([FromBody] LineTypeSaveRequest? request)
    {
        if (RequireProductionManager(LineTypeError) is { } error)
        {
            return error;
        }

        return request is null
            ? LineTypeError(400, "请求体不能为空")
            : LineTypeResponse(productionLineService.SaveLineType(request));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addProductionLine")]
    public IActionResult AddProductionLine([FromBody] ProductionLineCreateRequest? request)
    {
        if (RequireProductionManager(ProductionLineError) is { } error)
        {
            return error;
        }

        return request is null
            ? ProductionLineError(400, "请求体不能为空")
            : ProductionLineResponse(productionLineService.AddLine(request));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateProductionLine")]
    public IActionResult UpdateProductionLine([FromBody] ProductionLineUpdateRequest? request)
    {
        if (RequireProductionManager(ProductionLineError) is { } error)
        {
            return error;
        }

        return request is null
            ? ProductionLineError(400, "请求体不能为空")
            : ProductionLineResponse(productionLineService.UpdateLine(request));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listCapacityConfig")]
    public IActionResult ListCapacityConfig(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "type_id")] long? typeId)
    {
        if (RequireProductionManager(CapacityConfigPageError) is { } error)
        {
            return error;
        }

        (int currentPage, int size) = Paging.Normalize(page, pageSize);
        return CapacityConfigPage(
            capacityService.ListConfigs(currentPage, size, materialId, typeId));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("saveCapacityConfig")]
    public IActionResult SaveCapacityConfig([FromBody] CapacityConfigSaveRequest? request)
    {
        if (RequireProductionManager(CapacityConfigError) is { } error)
        {
            return error;
        }

        return request is null
            ? CapacityConfigError(400, "请求体不能为空")
            : CapacityConfigResponse(capacityService.SaveConfig(request));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listProductionCalendar")]
    public IActionResult ListProductionCalendar(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "line_id")] long? lineId,
        [FromQuery(Name = "calendar_date_start")] DateOnly? startDate,
        [FromQuery(Name = "calendar_date_end")] DateOnly? endDate,
        [FromQuery(Name = "config_id")] long? configId)
    {
        if (RequireProductionManager(ProductionCalendarPageError) is { } error)
        {
            return error;
        }

        (int currentPage, int size) = Paging.Normalize(page, pageSize);
        return ProductionCalendarPage(
            capacityService.ListCalendars(
                currentPage,
                size,
                lineId,
                startDate,
                endDate,
                configId));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("saveProductionCalendar")]
    public IActionResult SaveProductionCalendar([FromBody] ProductionCalendarSaveRequest? request)
    {
        if (RequireProductionManager(ProductionCalendarError) is { } error)
        {
            return error;
        }

        return request is null
            ? ProductionCalendarError(400, "请求体不能为空")
            : ProductionCalendarResponse(capacityService.SaveCalendar(request));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteProductionCalendar")]
    public IActionResult DeleteProductionCalendar([FromBody] ProductionCalendarDeleteRequest? request)
    {
        if (RequireProductionManager(ApiError) is { } error)
        {
            return error;
        }

        return request is null
            ? ApiError(400, "请求体不能为空")
            : ApiResponse(capacityService.DeleteCalendar(request));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("estimateProductionCapacity")]
    public IActionResult EstimateProductionCapacity(
        [FromBody] ProductionCapacityEstimateRequest? request)
    {
        if (RequireProductionManager(ProductionCapacityEstimateError) is { } error)
        {
            return error;
        }

        return request is null
            ? ProductionCapacityEstimateError(400, "请求体不能为空")
            : ProductionCapacityEstimateResponse(capacityService.Estimate(request));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("runCapacityDetection")]
    public IActionResult RunCapacityDetection([FromBody] CapacityDetectionRunRequest? request)
    {
        if (RequireProductionManager(CapacityDetectionError) is { } error)
        {
            return error;
        }

        return request is null
            ? CapacityDetectionError(400, "请求体不能为空")
            : CapacityDetectionResponse(capacityService.RunDetection(request));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("saveCapacityBalance")]
    public IActionResult SaveCapacityBalance([FromBody] CapacityBalanceSaveRequest? request)
    {
        CurrentUser? currentUser = ResolveCurrentUser();
        if (currentUser is null)
        {
            return CapacityBalanceError(401, "登录状态无效");
        }

        if (!currentUser.IsProductionManager)
        {
            return CapacityBalanceError(403, "无权保存产能平衡方案");
        }

        return request is null
            ? CapacityBalanceError(400, "请求体不能为空")
            : CapacityBalanceResponse(capacityService.SaveBalance(request, currentUser));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("reportProductionLineFault")]
    public IActionResult ReportProductionLineFault([FromBody] FaultRecordCreateRequest? request)
    {
        CurrentUser? currentUser = ResolveCurrentUser();
        if (currentUser is null)
        {
            return FaultRecordError(401, "登录状态无效");
        }

        return request is null
            ? FaultRecordError(400, "请求体不能为空")
            : FaultRecordResponse(productionLineService.ReportFault(request, currentUser));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateProductionLineFault")]
    public IActionResult UpdateProductionLineFault([FromBody] FaultRecordUpdateRequest? request)
    {
        CurrentUser? currentUser = ResolveCurrentUser();
        if (currentUser is null)
        {
            return FaultRecordError(401, "登录状态无效");
        }

        return request is null
            ? FaultRecordError(400, "请求体不能为空")
            : FaultRecordResponse(productionLineService.UpdateFault(request, currentUser));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateProductionLineStatus")]
    public IActionResult UpdateProductionLineStatus(
        [FromBody] ProductionLineStatusUpdateRequest? request)
    {
        CurrentUser? currentUser = ResolveCurrentUser();
        if (currentUser is null)
        {
            return ProductionLineStatusError(401, "登录状态无效");
        }

        return request is null
            ? ProductionLineStatusError(400, "请求体不能为空")
            : ProductionLineStatusResponse(
                productionLineService.UpdateLineStatus(request, currentUser));
    }

    private CurrentUser? ResolveCurrentUser() =>
        userContext.Resolve(User.GetEmployeeNo());

    private IActionResult? RequireProductionManager(Func<int, string, IActionResult> errorFactory)
    {
        CurrentUser? currentUser = ResolveCurrentUser();
        if (currentUser is null)
        {
            return errorFactory(401, "登录状态无效");
        }

        return currentUser.IsProductionManager
            ? null
            : errorFactory(403, "仅生产管理员或系统管理员可执行该操作");
    }

    private IActionResult LineTypePage(
        ProductionResourceResult<ProductionResourcePage<LineType>> result)
    {
        LineTypePageResponseAllOfData? data = result.Data is null
            ? null
            : new LineTypePageResponseAllOfData
            {
                Records = result.Data.Records,
                Total = result.Data.Total,
                Page = result.Data.Page,
                PageSize = result.Data.PageSize,
            };
        return Ok(new LineTypePageResponse
        {
            Code = (LineTypePageResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = data!,
        });
    }

    private IActionResult LineTypePageError(int code, string message) =>
        LineTypePage(ProductionResourceResult<ProductionResourcePage<LineType>>.Fail(code, message));

    private IActionResult LineTypeResponse(ProductionResourceResult<LineType> result) =>
        Ok(new LineTypeResponse
        {
            Code = (LineTypeResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult LineTypeError(int code, string message) =>
        LineTypeResponse(ProductionResourceResult<LineType>.Fail(code, message));

    private IActionResult ProductionLinePage(
        ProductionResourceResult<ProductionResourcePage<ProductionLine>> result)
    {
        ProductionLinePageResponseAllOfData? data = result.Data is null
            ? null
            : new ProductionLinePageResponseAllOfData
            {
                Records = result.Data.Records,
                Total = result.Data.Total,
                Page = result.Data.Page,
                PageSize = result.Data.PageSize,
            };
        return Ok(new ProductionLinePageResponse
        {
            Code = (ProductionLinePageResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = data!,
        });
    }

    private IActionResult ProductionLinePageError(int code, string message) =>
        ProductionLinePage(
            ProductionResourceResult<ProductionResourcePage<ProductionLine>>.Fail(code, message));

    private IActionResult ProductionLineResponse(ProductionResourceResult<ProductionLine> result) =>
        Ok(new ProductionLineResponse
        {
            Code = (ProductionLineResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult ProductionLineError(int code, string message) =>
        ProductionLineResponse(ProductionResourceResult<ProductionLine>.Fail(code, message));

    private IActionResult CapacityConfigPage(
        ProductionResourceResult<ProductionResourcePage<CapacityConfig>> result)
    {
        CapacityConfigPageResponseAllOfData? data = result.Data is null
            ? null
            : new CapacityConfigPageResponseAllOfData
            {
                Records = result.Data.Records,
                Total = result.Data.Total,
                Page = result.Data.Page,
                PageSize = result.Data.PageSize,
            };
        return Ok(new CapacityConfigPageResponse
        {
            Code = (CapacityConfigPageResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = data!,
        });
    }

    private IActionResult CapacityConfigPageError(int code, string message) =>
        CapacityConfigPage(
            ProductionResourceResult<ProductionResourcePage<CapacityConfig>>.Fail(code, message));

    private IActionResult CapacityConfigResponse(
        ProductionResourceResult<CapacityConfig> result) =>
        Ok(new CapacityConfigResponse
        {
            Code = (CapacityConfigResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult CapacityConfigError(int code, string message) =>
        CapacityConfigResponse(ProductionResourceResult<CapacityConfig>.Fail(code, message));

    private IActionResult ProductionCalendarPage(
        ProductionResourceResult<ProductionResourcePage<ProductionCalendar>> result)
    {
        ProductionCalendarPageResponseAllOfData? data = result.Data is null
            ? null
            : new ProductionCalendarPageResponseAllOfData
            {
                Records = result.Data.Records,
                Total = result.Data.Total,
                Page = result.Data.Page,
                PageSize = result.Data.PageSize,
            };
        return Ok(new ProductionCalendarPageResponse
        {
            Code = (ProductionCalendarPageResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = data!,
        });
    }

    private IActionResult ProductionCalendarPageError(int code, string message) =>
        ProductionCalendarPage(
            ProductionResourceResult<ProductionResourcePage<ProductionCalendar>>.Fail(
                code,
                message));

    private IActionResult ProductionCalendarResponse(
        ProductionResourceResult<ProductionCalendar> result) =>
        Ok(new ProductionCalendarResponse
        {
            Code = (ProductionCalendarResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult ProductionCalendarError(int code, string message) =>
        ProductionCalendarResponse(ProductionResourceResult<ProductionCalendar>.Fail(code, message));

    private IActionResult ApiResponse(ProductionResourceResult<object> result) =>
        Ok(new ApiResponse
        {
            Code = (ApiResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult ApiError(int code, string message) =>
        ApiResponse(ProductionResourceResult<object>.Fail(code, message));

    private IActionResult ProductionCapacityEstimateResponse(
        ProductionResourceResult<ProductionCapacityEstimateResult> result) =>
        Ok(new ProductionCapacityEstimateResponse
        {
            Code = (ProductionCapacityEstimateResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult ProductionCapacityEstimateError(int code, string message) =>
        ProductionCapacityEstimateResponse(
            ProductionResourceResult<ProductionCapacityEstimateResult>.Fail(code, message));

    private IActionResult CapacityDetectionResponse(
        ProductionResourceResult<CapacityDetection> result) =>
        Ok(new CapacityDetectionResponse
        {
            Code = (CapacityDetectionResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult CapacityDetectionError(int code, string message) =>
        CapacityDetectionResponse(ProductionResourceResult<CapacityDetection>.Fail(code, message));

    private IActionResult CapacityBalanceResponse(
        ProductionResourceResult<CapacityBalance> result) =>
        Ok(new CapacityBalanceResponse
        {
            Code = (CapacityBalanceResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult CapacityBalanceError(int code, string message) =>
        CapacityBalanceResponse(ProductionResourceResult<CapacityBalance>.Fail(code, message));

    private IActionResult FaultRecordResponse(ProductionResourceResult<FaultRecord> result) =>
        Ok(new FaultRecordResponse
        {
            Code = (FaultRecordResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult FaultRecordError(int code, string message) =>
        FaultRecordResponse(ProductionResourceResult<FaultRecord>.Fail(code, message));

    private IActionResult ProductionLineStatusResponse(
        ProductionResourceResult<ProductionLineStatus> result) =>
        Ok(new ProductionLineStatusResponse
        {
            Code = (ProductionLineStatusResponse.CodeEnum)result.Code,
            Message = result.Message,
            Data = result.Data!,
        });

    private IActionResult ProductionLineStatusError(int code, string message) =>
        ProductionLineStatusResponse(
            ProductionResourceResult<ProductionLineStatus>.Fail(code, message));
}
