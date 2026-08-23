using Backend.Services;

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
