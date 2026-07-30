using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>
/// Part B 共享的状态枚举与数据库中文状态字符串之间的映射。
/// 数据库各表 status 列存中文，API 契约使用英文枚举。
/// </summary>
public static class InventoryAlertStatusMap
{
    public static class Db
    {
        public const string Pending = "待处理";
        public const string Handled = "已处理";
        public const string Ignored = "已忽略";
    }

    private static readonly Dictionary<string, InventoryAlertStatus> DbToEnum = new()
    {
        [Db.Pending] = InventoryAlertStatus.PendingEnum,
        [Db.Handled] = InventoryAlertStatus.HandledEnum,
        [Db.Ignored] = InventoryAlertStatus.IgnoredEnum,
    };

    private static readonly Dictionary<InventoryAlertStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static InventoryAlertStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : InventoryAlertStatus.PendingEnum;

    public static string ToDb(InventoryAlertStatus status) => EnumToDb[status];

    public static string? ToDbOrNull(InventoryAlertStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}

public static class StockLockStatusMap
{
    public static class Db
    {
        public const string Locked = "已锁定";
        public const string Cancelled = "已取消";
        public const string Consumed = "已消耗";
    }

    private static readonly Dictionary<string, StockLockStatus> DbToEnum = new()
    {
        [Db.Locked] = StockLockStatus.LockedEnum,
        [Db.Cancelled] = StockLockStatus.CancelledEnum,
        [Db.Consumed] = StockLockStatus.ConsumedEnum,
    };

    private static readonly Dictionary<StockLockStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static StockLockStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : StockLockStatus.LockedEnum;

    public static string ToDb(StockLockStatus status) => EnumToDb[status];

    public static string? ToDbOrNull(StockLockStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}

public static class ObsoleteMaterialStatusMap
{
    public static class Db
    {
        public const string Pending = "待处理";
        public const string Handled = "已处理";
        public const string Ignored = "已忽略";
    }

    private static readonly Dictionary<string, ObsoleteMaterialStatus> DbToEnum = new()
    {
        [Db.Pending] = ObsoleteMaterialStatus.PendingEnum,
        [Db.Handled] = ObsoleteMaterialStatus.HandledEnum,
        [Db.Ignored] = ObsoleteMaterialStatus.IgnoredEnum,
    };

    private static readonly Dictionary<ObsoleteMaterialStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static ObsoleteMaterialStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : ObsoleteMaterialStatus.PendingEnum;

    public static string ToDb(ObsoleteMaterialStatus status) => EnumToDb[status];

    public static string? ToDbOrNull(ObsoleteMaterialStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}

public static class PurchaseOrderStatusMap
{
    public static class Db
    {
        public const string Draft = "草稿";
        public const string Submitted = "已提交";
        public const string PartialReceived = "部分到货";
        public const string Completed = "已完成";
        public const string Cancelled = "已取消";
    }

    private static readonly Dictionary<string, PurchaseOrderStatus> DbToEnum = new()
    {
        [Db.Draft] = PurchaseOrderStatus.DraftEnum,
        [Db.Submitted] = PurchaseOrderStatus.SubmittedEnum,
        [Db.PartialReceived] = PurchaseOrderStatus.PartialReceivedEnum,
        [Db.Completed] = PurchaseOrderStatus.CompletedEnum,
        [Db.Cancelled] = PurchaseOrderStatus.CancelledEnum,
    };

    private static readonly Dictionary<PurchaseOrderStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static PurchaseOrderStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : PurchaseOrderStatus.DraftEnum;

    public static string ToDb(PurchaseOrderStatus status) => EnumToDb[status];

    public static string? ToDbOrNull(PurchaseOrderStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}

public static class PurchaseOverdueReminderStatusMap
{
    public static class Db
    {
        public const string PendingUrge = "待催交";
        public const string Urged = "已催交";
        public const string Received = "已到货";
    }

    private static readonly Dictionary<string, PurchaseOverdueReminderStatus> DbToEnum = new()
    {
        [Db.PendingUrge] = PurchaseOverdueReminderStatus.PendingUrgeEnum,
        [Db.Urged] = PurchaseOverdueReminderStatus.UrgedEnum,
        [Db.Received] = PurchaseOverdueReminderStatus.ReceivedEnum,
    };

    private static readonly Dictionary<PurchaseOverdueReminderStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static PurchaseOverdueReminderStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : PurchaseOverdueReminderStatus.PendingUrgeEnum;

    public static string ToDb(PurchaseOverdueReminderStatus status) => EnumToDb[status];

    public static string? ToDbOrNull(PurchaseOverdueReminderStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}
