using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Booking;

/// <param name="Slots">One entry per requested slot, in the order of the request.</param>
/// <param name="BookingsToRemove">Existing bookings an overbooking play mode removes (or would remove, for a dry run).</param>
/// <param name="CreatedBookingIds">The ids of the new bookings, in the order of the slots. Empty for a dry run.</param>
public record BookCourtsResult(
    List<BookingSlotCheck> Slots,
    List<BookingToRemoveDto> BookingsToRemove,
    List<int> CreatedBookingIds);

/// <param name="Index">Position of the slot in the request.</param>
/// <param name="ErrorCode">Name of the error code that rejects the slot (same codes as a single booking), <c>null</c> when the slot is valid.</param>
/// <param name="ErrorDetails">Arguments of the error message, like the error details of an error response.</param>
public record BookingSlotCheck(
    int Index,
    string? ErrorCode,
    string[] ErrorDetails);

public record BookingToRemoveDto(
    int BookingEntryId,
    int CourtId,
    DateTimeOffsetInterval Interval,
    string PlayModeName,
    List<string> PlayerNames);
