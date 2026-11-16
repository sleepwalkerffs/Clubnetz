using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Booking;

// CA1720: 'Single' contains a type name but it is the correct domain term for
// "only this one occurrence" in a recurring booking context.
#pragma warning disable CA1720
public enum RecurringEditScope
{
    Single,
    ThisAndFollowing
}
#pragma warning restore CA1720

/// <remarks><c>TimeZoneInfoId</c>: Ignored, kept for older clients: the server uses the club's time zone (<c>Club.TimeZoneId</c>).</remarks>
public record EditRecurringBookingModel(
    [Required] int BookingEntryId,
    [Required] int CourtId,
    [Required] DateTimeOffset From,
    [Required] DateTimeOffset To,
    string? TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    [Required] RecurringEditScope Scope,
    int? RecurrenceIntervalWeeks,
    DateOnly? EndDate,
    string? Comment);
