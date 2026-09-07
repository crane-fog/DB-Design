namespace Backend.Filters;

/// <summary>需要读取数据库真实状态的审计快照类型。</summary>
public enum OperationAuditSnapshotKind
{
    None,
    User,
    Role,
    UserRoles,
    RolePermissions,
    MaterialCategory,
    Material,
    BomVersion,
    Bom,
    ExternalOrder,
    StockAlert,
    StockLockByOrder,
    StockLockById,
    WasteDetection,
    ProductionOrder,
    LineType,
    ProductionLine,
    CapacityConfig,
    ProductionCalendar,
    FaultRecord,
    LineStatus,
    PurchaseOrder,
    OverdueReminder,
    BatchConsumption,
}

/// <summary>
/// 标记需要自动记录操作日志的 Controller Action。
/// 未指定快照类型时，审计 Filter 会把请求参数和响应 data 记录到 after_data；
/// 指定快照类型时，Filter 会在 Action 执行前后读取数据库真实状态。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OperationAuditAttribute(
    string module,
    string action,
    OperationAuditSnapshotKind snapshotKind = OperationAuditSnapshotKind.None) : Attribute
{
    public string Module { get; } = module;

    public string Action { get; } = action;

    public OperationAuditSnapshotKind SnapshotKind { get; } = snapshotKind;
}
