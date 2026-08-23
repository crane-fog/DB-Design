using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

// A/B 内部集成契约类型（见 A-B_内部集成契约说明.md）。
// 与 A 侧（MaterialCatalogService / DemandAnalysisService）中的定义保持一致，
// 用于 A 模块合入前 B 侧独立编译；A 合入后应删除本文件以避免重复定义。

/// <summary>B 模块库存快照契约。A 用它为物料详情补充库存信息。</summary>
public interface IStockReadQuery
{
    IReadOnlyDictionary<long, StockSnapshot> GetSnapshots(
        IReadOnlyCollection<long> materialIds);
}

/// <summary>B 模块库存初始化契约。调用方拥有事务。</summary>
public interface IStockInitialization
{
    void EnsureStockRecord(
        OracleConnection connection,
        OracleTransaction transaction,
        long materialId);
}

public sealed record StockSnapshot(
    long MaterialId,
    decimal AvailableQty,
    decimal LockedQty,
    DateTime? LastInDate,
    DateTime? LastOutDate);

/// <summary>B 模块报价契约。缺失报价不得回退到最低价或其他供应商。</summary>
public interface IPriceQuery
{
    IReadOnlyDictionary<long, EffectivePriceResult> GetEffectivePrices(
        IReadOnlyCollection<long> materialIds,
        DateOnly pricingDate);
}

public sealed record EffectivePriceResult(
    long MaterialId,
    long? SupplierId,
    decimal? Price,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool Missing,
    string? MissingReason);

/// <summary>A 模块的 BOM 需求展开契约，供 B/C 使用；传入的事务由调用方管理。</summary>
public interface IBomExpansionQuery
{
    IReadOnlyList<BomDemandExpansionItem> ExpandDemand(
        long materialId,
        long versionId,
        decimal quantity,
        OracleConnection? connection = null,
        OracleTransaction? transaction = null);
}

public sealed record BomDemandExpansionItem(
    long MaterialId,
    string MaterialName,
    string MaterialType,
    decimal NetQuantity,
    decimal GrossQuantity,
    decimal LossRate,
    int Depth,
    string Path,
    bool IsLeaf);
