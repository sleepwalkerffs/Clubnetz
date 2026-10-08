namespace Bookennis.Api.Business.Statistics;

/// <summary>
/// Converts the UTC booking intervals stored in the database to the local time of the booking.
/// Statistics about hours, days and seasons must be evaluated in local time, otherwise e.g. a weekly
/// series at 18:00 would be split across two hours by DST changes.
/// </summary>
internal sealed class LocalTimeConverter
{
    private readonly Dictionary<string, TimeZoneInfo> timeZones = new();

    public DateTime ToLocal(DateTimeOffset value, string timeZoneInfoId)
        => TimeZoneInfo.ConvertTimeFromUtc(value.UtcDateTime, GetTimeZone(timeZoneInfoId));

    private TimeZoneInfo GetTimeZone(string timeZoneInfoId)
    {
        if (timeZones.TryGetValue(timeZoneInfoId, out var tz))
            return tz;

        tz = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneInfoId, out var found) ? found : TimeZoneInfo.Utc;
        timeZones[timeZoneInfoId] = tz;
        return tz;
    }
}
