using System.Security.Claims;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>
/// C 模块共享的状态枚举与数据库中文状态字符串之间的映射，
/// 以及从 ClaimsPrincipal 读取登录工号的工具方法。
/// 数据库 production_order.status 存中文（待审核/待排产/生产中/已完工/已取消），
/// 而 API 契约使用英文枚举（pending_review 等），此处集中转换避免散落各处。
/// </summary>
public static class ProductionStatusMap
{
    /// <summary>数据库 production_order.status 的中文常量，作为全模块唯一状态字面量来源。</summary>
    public static class Db
    {
        public const string PendingReview = "待审核";
        public const string PendingSchedule = "待排产";
        public const string InProgress = "生产中";
        public const string Completed = "已完工";
        public const string Cancelled = "已取消";
    }

    private static readonly Dictionary<string, ProductionOrderStatus> DbToEnum = new()
    {
        [Db.PendingReview] = ProductionOrderStatus.PendingReviewEnum,
        [Db.PendingSchedule] = ProductionOrderStatus.PendingScheduleEnum,
        [Db.InProgress] = ProductionOrderStatus.InProgressEnum,
        [Db.Completed] = ProductionOrderStatus.CompletedEnum,
        [Db.Cancelled] = ProductionOrderStatus.CancelledEnum,
    };

    private static readonly Dictionary<ProductionOrderStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static ProductionOrderStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : ProductionOrderStatus.PendingReviewEnum;

    public static string ToDb(ProductionOrderStatus status) => EnumToDb[status];

    /// <summary>将 API 查询参数中的英文枚举转换为数据库中文状态，无法识别时返回 null。</summary>
    public static string? ToDbOrNull(ProductionOrderStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}

/// <summary>
/// 外部订单状态映射：数据库中文（待审核/已接受/已拒绝）与英文枚举（pending_review/accepted/rejected）。
/// </summary>
public static class ExternalOrderStatusMap
{
    /// <summary>数据库 external_order.status 的中文常量，作为全模块唯一状态字面量来源。</summary>
    public static class Db
    {
        public const string PendingReview = "待审核";
        public const string Accepted = "已接受";
        public const string Converted = "已转换";
        public const string Rejected = "已拒绝";
    }

    private static readonly Dictionary<string, ExternalOrderStatus> DbToEnum = new()
    {
        [Db.PendingReview] = ExternalOrderStatus.PendingReviewEnum,
        [Db.Accepted] = ExternalOrderStatus.AcceptedEnum,
        [Db.Converted] = ExternalOrderStatus.ConvertedEnum,
        [Db.Rejected] = ExternalOrderStatus.RejectedEnum,
    };

    private static readonly Dictionary<ExternalOrderStatus, string> EnumToDb =
        DbToEnum.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static ExternalOrderStatus FromDb(string dbStatus) =>
        DbToEnum.TryGetValue(dbStatus, out var value) ? value : ExternalOrderStatus.PendingReviewEnum;

    public static string ToDb(ExternalOrderStatus status) => EnumToDb[status];

    public static string? ToDbOrNull(ExternalOrderStatus? status) =>
        status.HasValue && EnumToDb.TryGetValue(status.Value, out var db) ? db : null;
}

/// <summary>
/// 读取 JWT 中登录工号的扩展方法。AuthService.CreateToken 只写入 employee_no claim。
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static string? GetEmployeeNo(this ClaimsPrincipal principal) =>
        principal.FindFirst("employee_no")?.Value;
}

/// <summary>
/// 分页参数归一化：契约默认 page=1、page_size=10，且均不小于 1。
/// </summary>
public static class Paging
{
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page is > 0 ? page.Value : 1;
        var normalizedSize = pageSize is > 0 ? pageSize.Value : 10;
        return (normalizedPage, normalizedSize);
    }
}
