namespace Bookennis.Domain.Clubs;

/// <summary>
/// The times of day a club sets (opening hours, prime time, times of club events) are wall-clock times of the club,
/// not UTC: 09:00 stays 09:00 when the clocks change for daylight saving time. Every club has its own time zone
/// (<see cref="Club.TimeZoneId"/>, an IANA id); the clubs that existed before it was a setting are in <see cref="DefaultId"/>.
/// </summary>
public static class ClubTimeZone
{
    public const string DefaultId = "Europe/Vienna";

    public static readonly TimeZoneInfo Default = TimeZoneInfo.FindSystemTimeZoneById(DefaultId);

    /// <summary>The time zone with the IANA id <paramref name="timeZoneId"/>; <see cref="Default"/> for an unknown or empty id.</summary>
    public static TimeZoneInfo Get(string? timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : Default;

    public static bool IsValid(string? timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    /// <summary>The wall-clock time of the club at <paramref name="value"/>.</summary>
    public static DateTime ToLocal(DateTimeOffset value, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTimeFromUtc(value.UtcDateTime, timeZone);

    /// <summary>The local day of the club at <paramref name="now"/> (not the UTC day: in New York the UTC day changes at 19:00 or 20:00).</summary>
    public static DateOnly Today(TimeZoneInfo timeZone, DateTimeOffset now) => DateOnly.FromDateTime(ToLocal(now, timeZone));

    public static DateOnly Today(TimeZoneInfo timeZone) => Today(timeZone, DateTimeOffset.UtcNow);

    /// <summary>The moment the local day <paramref name="date"/> starts in the club.</summary>
    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo timeZone)
        => At(date, TimeOnly.MinValue, timeZone);

    /// <summary>The moment of the wall-clock time <paramref name="time"/> on <paramref name="date"/> in the club (a time skipped by daylight saving time is moved forward).</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
            local = local.AddMinutes(30);
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }
}
