using Org.OpenAPITools.Models;

namespace Backend.Services.Interfaces;

/// <summary>
/// B 模块提供给 C 模块的窄接口：完工入库时联动库存。
/// </summary>
public interface IStockOperationService
{
    CompletionInboundResult RecordFinishInbound(
        long orderId,
        long materialId,
        long versionId,
        decimal finishQty,
        decimal qualifiedQty,
        string batchNo,
        long operatorId);
}

public sealed record CompletionInboundResult(
    bool Ok,
    CompletionInboundOrder? Order,
    int ErrorCode,
    string? ErrorMessage)
{
    public static CompletionInboundResult Success(CompletionInboundOrder order) =>
        new(true, order, 200, null);
    public static CompletionInboundResult Fail(int code, string message) =>
        new(false, null, code, message);
}
