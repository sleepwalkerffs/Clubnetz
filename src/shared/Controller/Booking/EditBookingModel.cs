using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Booking;

public record EditBookingModel(
    [Required] int BookingEntryId,
    [Required] int CourtId,
    [Required] DateTimeOffset From,
    [Required] DateTimeOffset To,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment);
