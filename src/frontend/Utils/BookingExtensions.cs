using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Client.Utils;

public static class BookingExtensions
{
    public static List<BookingResult> AsLocalTime(this List<BookingResult> bookings)
        => bookings;//.ConvertAll(booking => booking.AsLocalTime());

    public static BookingResult AsLocalTime(this BookingResult booking)
        => booking;// with { From = booking.From.ToLocalTime(), To = booking.To.ToLocalTime() };
}
