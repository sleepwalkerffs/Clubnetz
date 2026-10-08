using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Booking;

public record BookRecurringCourtModel(
    [Required] int CourtId,
    [Required] DateTimeOffset From,
    [Required] DateTimeOffset To,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    [Required] int RecurrenceIntervalWeeks,
    DateOnly? EndDate,
    string? Comment);
