using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

/// <summary>
/// 质量追溯接口（C 模块）。各接口分别检查稳定权限码。
/// </summary>
[ApiController]
[Authorize]
[Route("/api")]
public class QualityTraceController(
    QualityTraceService traceService,
    AuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listBatchConsumption")]
    public IActionResult ListConsumption(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "order_id")] long? orderId,
        [FromQuery(Name = "item_id")] long? itemId,
        [FromQuery(Name = "material_id")] long? materialId)
    {
        AuthResult auth = Authorize(PermissionCode.TraceConsumptionViewEnum);
        if (!auth.Ok)
        {
            return Ok(PageResp((BatchConsumptionPageResponse.CodeEnum)auth.Code, auth.Message ?? "无权查询批次消耗", null));
        }

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = traceService.ListConsumption(currentPage, size, orderId, itemId, materialId);

        return Ok(new BatchConsumptionPageResponse
        {
            Code = BatchConsumptionPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new BatchConsumptionPageResponseAllOfData
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
    [Route("getBatchConsumption")]
    public IActionResult GetConsumption([FromQuery(Name = "consumption_id")] long consumptionId)
    {
        AuthResult auth = Authorize(PermissionCode.TraceConsumptionViewEnum);
        if (!auth.Ok)
        {
            return Ok(Single((BatchConsumptionResponse.CodeEnum)auth.Code, auth.Message ?? "无权查询批次消耗", null));
        }

        var consumption = traceService.GetConsumption(consumptionId);
        return consumption is null
            ? Ok(Single(BatchConsumptionResponse.CodeEnum._404Enum, "批次消耗关系不存在", null))
            : Ok(Single(BatchConsumptionResponse.CodeEnum._200Enum, "查询成功", consumption));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addBatchConsumption")]
    public IActionResult AddConsumption([FromBody] BatchConsumptionCreateRequest? request)
    {
        AuthResult auth = Authorize(PermissionCode.TraceConsumptionCreateEnum);
        if (!auth.Ok)
        {
            return Ok(Single((BatchConsumptionResponse.CodeEnum)auth.Code, auth.Message ?? "无权新增批次消耗", null));
        }

        if (request is null)
        {
            return Ok(Single(BatchConsumptionResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(traceService.AddConsumption(request), "新增成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateBatchConsumption")]
    public IActionResult UpdateConsumption([FromBody] BatchConsumptionUpdateRequest? request)
    {
        AuthResult auth = Authorize(PermissionCode.TraceConsumptionUpdateEnum);
        if (!auth.Ok)
        {
            return Ok(Single((BatchConsumptionResponse.CodeEnum)auth.Code, auth.Message ?? "无权修改批次消耗", null));
        }

        if (request is null)
        {
            return Ok(Single(BatchConsumptionResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromResult(traceService.UpdateConsumption(request), "修改成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteBatchConsumption")]
    public IActionResult DeleteConsumption([FromBody] BatchConsumptionDeleteRequest? request)
    {
        AuthResult auth = Authorize(PermissionCode.TraceConsumptionDeleteEnum);
        if (!auth.Ok)
        {
            return Ok(Envelope((ApiResponse.CodeEnum)auth.Code, auth.Message ?? "无权删除批次消耗"));
        }

        if (request is null)
        {
            return Ok(Envelope(ApiResponse.CodeEnum._400Enum, "请求体不能为空"));
        }

        var result = traceService.DeleteConsumption(request.ConsumptionId);
        var code = result.Ok ? ApiResponse.CodeEnum._200Enum : (ApiResponse.CodeEnum)result.ErrorCode;
        return Ok(Envelope(code, result.Ok ? "删除成功" : result.ErrorMessage ?? "删除失败"));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("traceProductBatch")]
    public IActionResult TraceProductBatch(
        [FromQuery(Name = "order_id")] long? orderId,
        [FromQuery(Name = "batch_no")] string? batchNo,
        [FromQuery(Name = "include_supplier")] bool? includeSupplier)
    {
        AuthResult auth = Authorize(PermissionCode.TraceProductViewEnum);
        if (!auth.Ok)
        {
            return Ok(ProductResp((ProductBatchTraceResponse.CodeEnum)auth.Code, auth.Message ?? "无权追溯成品批次", null));
        }

        if (orderId is null && string.IsNullOrWhiteSpace(batchNo))
        {
            return Ok(ProductResp(ProductBatchTraceResponse.CodeEnum._400Enum, "order_id 和 batch_no 至少提供一个", null));
        }

        var result = traceService.TraceProductBatch(orderId, batchNo, includeSupplier ?? true);
        return result is null
            ? Ok(ProductResp(ProductBatchTraceResponse.CodeEnum._404Enum, "未找到对应成品批次", null))
            : Ok(ProductResp(ProductBatchTraceResponse.CodeEnum._200Enum, "追溯成功", result));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("traceMaterialBatch")]
    public IActionResult TraceMaterialBatch(
        [FromQuery(Name = "item_id")] long? itemId,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "supplier_id")] long? supplierId,
        [FromQuery(Name = "receive_date_start")] DateOnly? receiveDateStart,
        [FromQuery(Name = "receive_date_end")] DateOnly? receiveDateEnd)
    {
        AuthResult auth = Authorize(PermissionCode.TraceMaterialViewEnum);
        if (!auth.Ok)
        {
            return Ok(MaterialResp((MaterialBatchTraceResponse.CodeEnum)auth.Code, auth.Message ?? "无权追溯原材料批次", null));
        }

        var hasDateRange = receiveDateStart.HasValue && receiveDateEnd.HasValue;
        if (itemId is null && materialId is null && supplierId is null && !hasDateRange)
        {
            return Ok(MaterialResp(
                MaterialBatchTraceResponse.CodeEnum._400Enum,
                "item_id、material_id、supplier_id 或到货日期范围至少提供一种",
                null));
        }

        var results = traceService.TraceMaterialBatch(
            itemId,
            materialId,
            supplierId,
            receiveDateStart,
            receiveDateEnd);
        return Ok(MaterialResp(MaterialBatchTraceResponse.CodeEnum._200Enum, "追溯成功", results));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("analyzeQualityImpact")]
    public IActionResult AnalyzeImpact([FromBody] QualityImpactAnalyzeRequest? request)
    {
        AuthResult auth = Authorize(PermissionCode.TraceImpactAnalyzeEnum);
        if (!auth.Ok)
        {
            return Ok(ImpactResp((QualityImpactAnalyzeResponse.CodeEnum)auth.Code, auth.Message ?? "无权执行质量影响分析", null));
        }

        var hasItemIds = request?.ItemIds is { Count: > 0 };
        var hasMaterial = request is { MaterialId: not 0 };
        var hasDateRange = request is not null
            && request.ReceiveDateStart != default
            && request.ReceiveDateEnd != default;
        if (request is null || (!hasItemIds && !hasMaterial && !hasDateRange))
        {
            return Ok(ImpactResp(
                QualityImpactAnalyzeResponse.CodeEnum._400Enum,
                "item_ids、material_id 或到货日期范围至少提供一种",
                null));
        }

        var result = traceService.AnalyzeImpact(request);
        return Ok(ImpactResp(QualityImpactAnalyzeResponse.CodeEnum._200Enum, "分析成功", result));
    }

    private AuthResult Authorize(PermissionCode permissionCode) =>
        authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);

    private IActionResult FromResult(BatchConsumptionResult result, string successMessage)
    {
        if (result.Ok)
        {
            return Ok(Single(BatchConsumptionResponse.CodeEnum._200Enum, successMessage, result.Record));
        }

        var code = (BatchConsumptionResponse.CodeEnum)result.ErrorCode;
        return Ok(Single(code, result.ErrorMessage ?? "操作失败", null));
    }

    private static BatchConsumptionResponse Single(
        BatchConsumptionResponse.CodeEnum code,
        string message,
        BatchConsumption? data) => new() { Code = code, Message = message, Data = data! };

    private static BatchConsumptionPageResponse PageResp(
        BatchConsumptionPageResponse.CodeEnum code,
        string message,
        BatchConsumptionPageResponseAllOfData? data) => new() { Code = code, Message = message, Data = data! };

    private static ProductBatchTraceResponse ProductResp(
        ProductBatchTraceResponse.CodeEnum code,
        string message,
        ProductBatchTraceResult? data) => new() { Code = code, Message = message, Data = data! };

    private static MaterialBatchTraceResponse MaterialResp(
        MaterialBatchTraceResponse.CodeEnum code,
        string message,
        List<MaterialBatchTraceResult>? data) => new() { Code = code, Message = message, Data = data! };

    private static QualityImpactAnalyzeResponse ImpactResp(
        QualityImpactAnalyzeResponse.CodeEnum code,
        string message,
        QualityImpactAnalyzeResult? data) => new() { Code = code, Message = message, Data = data! };

    private static ApiResponse Envelope(ApiResponse.CodeEnum code, string message) =>
        new() { Code = code, Message = message, Data = null };
}
