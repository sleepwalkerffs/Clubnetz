using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Shared.Controller.Members;

public record GetMemberBookingHistoryResult
{
    public required List<BookingResult> Bookings { get; init; }
}
