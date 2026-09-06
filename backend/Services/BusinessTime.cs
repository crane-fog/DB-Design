namespace Backend.Services;

/// <summary>时间点使用 UTC；业务日期、班次使用北京时间。</summary>
internal static class BusinessTime
{
    private static readonly TimeZoneInfo Beijing = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    public static DateOnly Today => ToDate(DateTime.UtcNow);

    public static DateOnly ToDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Beijing));

    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), Beijing);
}
