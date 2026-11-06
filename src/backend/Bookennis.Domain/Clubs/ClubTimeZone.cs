namespace Bookennis.Domain.Clubs;

/// <summary>
/// The times of day a club sets (opening hours, prime time, times of club events) are wall-clock times of the club,
/// not UTC: 09:00 stays 09:00 when the clocks change for daylight saving time.
/// The clubs are in Austria, Germany and Switzerland, which share one time zone.
/// </summary>
public static class ClubTimeZone
{
    public const string Id = "Europe/Vienna";

    public static readonly TimeZoneInfo Instance = TimeZoneInfo.FindSystemTimeZoneById(Id);

    /// <summary>The wall-clock time of the club at <paramref name="value"/>.</summary>
    public static DateTime ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTimeFromUtc(value.UtcDateTime, Instance);
}
