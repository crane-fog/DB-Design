namespace Backend.Services.Interfaces;

/// <summary>
/// B 模块提供给 A 模块的窄接口：查询物料的有效报价。
/// </summary>
public interface IPriceQueryService
{
    decimal? GetCurrentPrice(long materialId);
    Dictionary<long, decimal?> GetCurrentPrices(IEnumerable<long> materialIds);
}
