namespace Bookennis.Global;

public static class TimeOnlyExtensions
{
    public static TimeOnly AddTimeZoneInfo(this TimeOnly dateTime, string timeZoneInfoId)
        => AddTimeZoneInfo(dateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneInfoId));

    public static TimeOnly AddTimeZoneInfo(this TimeOnly dateTime, TimeZoneInfo timeZoneInfo)
        => dateTime.Add(timeZoneInfo.GetUtcOffset(DateTime.UtcNow));
}