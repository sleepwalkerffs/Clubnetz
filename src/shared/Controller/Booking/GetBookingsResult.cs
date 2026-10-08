using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Shared.Controller.Booking;

public record GetBookingsResult(List<BookingResult> Bookings);