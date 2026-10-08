namespace Bookennis.Global;

public static class DateTimeOffsetExtensions
{
    public static DateTimeOffset AddTimeZoneInfo(this DateTimeOffset dateTime, string timeZoneInfoId)
        => AddTimeZoneInfo(dateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneInfoId));

    public static DateTimeOffset AddTimeZoneInfo(this DateTimeOffset dateTime, TimeZoneInfo timeZoneInfo)
        => TimeZoneInfo.ConvertTime(dateTime, timeZoneInfo);
}