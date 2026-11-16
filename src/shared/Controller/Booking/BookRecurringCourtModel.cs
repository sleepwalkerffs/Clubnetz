using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Booking;

/// <remarks><c>TimeZoneInfoId</c>: Ignored, kept for older clients: the server uses the club's time zone (<c>Club.TimeZoneId</c>).</remarks>
public record BookRecurringCourtModel(
    [Required] int CourtId,
    [Required] DateTimeOffset From,
    [Required] DateTimeOffset To,
    string? TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    [Required] int RecurrenceIntervalWeeks,
    DateOnly? EndDate,
    string? Comment);
