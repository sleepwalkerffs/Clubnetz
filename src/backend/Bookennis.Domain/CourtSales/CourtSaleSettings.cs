using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.CourtSales;

/// <summary>
/// How a club sells its free court time to members who are not enrolled in the active season. One row per club;
/// without a row the club sells nothing (<see cref="IsEnabled"/> is <c>false</c> by default).
/// </summary>
[Guid("A6C3F0E2-4D1B-4E8A-9B37-2F5C8D1E7A40")]
public class CourtSaleSettings : TenantDomainEntity, IAggregateRoot
{
    public const string DefaultTimeZoneId = "Europe/Vienna";
    public const int DefaultMaxBookingMinutes = 120;
    public const int DefaultMaxDaysInAdvance = 14;
    public const int DefaultMaxUpcomingBookings = 1;

    public const int MinBookingMinutes = 30;
    public const int MaxBookingMinutesLimit = 240;
    public const int MaxDaysInAdvanceLimit = 90;
    public const int MaxUpcomingBookingsLimit = 10;
    public const int MaxTimeZoneIdLength = 100;

    /// <summary>Bought slots start and end on a full or half hour and last a multiple of it.</summary>
    public const int SlotMinutes = 30;

    /// <summary>One credit pays for one booking of up to this many minutes; a longer booking pays the rest online.</summary>
    public const int MinutesCoveredByCredit = 60;

#pragma warning disable CS8618
    private CourtSaleSettings() { }
#pragma warning restore CS8618

    public CourtSaleSettings(int clubId)
    {
        ClubId = clubId;
        TimeZoneId = DefaultTimeZoneId;
        MaxBookingMinutes = DefaultMaxBookingMinutes;
        MaxDaysInAdvance = DefaultMaxDaysInAdvance;
        MaxUpcomingBookings = DefaultMaxUpcomingBookings;
    }

    public bool IsEnabled { get; private set; }

    /// <summary>The play mode of the bookings that are bought. Required to switch the sale on.</summary>
    public int? PlayModeId { get; private set; }

    /// <summary>The prices are local times of this time zone. Decided by the club, never by the browser of the buyer.</summary>
    public string TimeZoneId { get; private set; }

    public int MaxBookingMinutes { get; private set; }
    public int MaxDaysInAdvance { get; private set; }

    /// <summary>How many bought bookings a member may have that are not over yet.</summary>
    public int MaxUpcomingBookings { get; private set; }

    public record SettingsData(bool IsEnabled, int? PlayModeId, string? TimeZoneId, int MaxBookingMinutes, int MaxDaysInAdvance, int MaxUpcomingBookings);

    public void Update(SettingsData data)
    {
        if (data.IsEnabled && data.PlayModeId is null)
            throw new PreconditionException(CourtSaleErrorCode.CourtSalePlayModeRequired, "A play mode is required to sell courts.");

        var timeZone = FindTimeZone(data.TimeZoneId);

        if (data.MaxBookingMinutes < MinBookingMinutes || data.MaxBookingMinutes > MaxBookingMinutesLimit || data.MaxBookingMinutes % SlotMinutes != 0)
            throw new PreconditionException(CourtSaleErrorCode.CourtSaleInvalidMaxBookingMinutes, [MinBookingMinutes.ToString(), MaxBookingMinutesLimit.ToString()], "Invalid maximum booking length.");

        if (data.MaxDaysInAdvance < 0 || data.MaxDaysInAdvance > MaxDaysInAdvanceLimit)
            throw new PreconditionException(CourtSaleErrorCode.CourtSaleInvalidMaxDaysInAdvance, ["0", MaxDaysInAdvanceLimit.ToString()], "Invalid number of days in advance.");

        if (data.MaxUpcomingBookings < 1 || data.MaxUpcomingBookings > MaxUpcomingBookingsLimit)
            throw new PreconditionException(CourtSaleErrorCode.CourtSaleInvalidMaxUpcomingBookings, ["1", MaxUpcomingBookingsLimit.ToString()], "Invalid number of upcoming bookings.");

        IsEnabled = data.IsEnabled;
        PlayModeId = data.PlayModeId;
        TimeZoneId = timeZone.Id;
        MaxBookingMinutes = data.MaxBookingMinutes;
        MaxDaysInAdvance = data.MaxDaysInAdvance;
        MaxUpcomingBookings = data.MaxUpcomingBookings;
    }

    /// <summary>The play mode was deleted: nothing can be sold until another one is chosen.</summary>
    public void RemovePlayMode()
    {
        PlayModeId = null;
        IsEnabled = false;
    }

    public TimeZoneInfo GetTimeZone() => FindTimeZone(TimeZoneId);

    public static TimeZoneInfo FindTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > MaxTimeZoneIdLength)
            throw new PreconditionException(CourtSaleErrorCode.CourtSaleInvalidTimeZone, "Invalid time zone.");

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new PreconditionException(CourtSaleErrorCode.CourtSaleInvalidTimeZone, "Invalid time zone.");
        }
    }
}
