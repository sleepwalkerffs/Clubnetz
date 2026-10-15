using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Booking;

/// <summary>
/// Books several time slots at once with the same play mode, players and comment (the multi-day planner).
/// All slots are validated with the same rules as a single booking; nothing is saved unless every slot is valid.
/// With <see cref="DryRun"/> nothing is saved and the result tells which slots would fail and which bookings an overbooking play mode would remove.
/// With <see cref="ExpectedBookingIdsToRemove"/> (the bookings the user confirmed after a dry run) the save is rejected when an overbooking play mode would remove other bookings than these.
/// </summary>
public record BookCourtsModel(
    [Required] List<BookingSlotModel> Slots,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    bool DryRun,
    List<int>? ExpectedBookingIdsToRemove = null);

public record BookingSlotModel(
    [Required] int CourtId,
    [Required] DateTimeOffset From,
    [Required] DateTimeOffset To);
