using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Client.Services.HttpClients.Bookings;

public interface IBookingsHttpClient
{
    public Task<HttpResult<BookingResult>> GetBooking(int bookingId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetBookingsResult>> GetBookings(int clubId, DateOnly dayFrom, DateOnly dayTo, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetUpcomingBookingsResult>> GetUpcomingBookings(int amount, CancellationToken cancellationToken = default);
    public Task<HttpResult<BookCourtResult>> BookCourt(BookCourtModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> EditBooking(int bookingId, EditBookingModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteBooking(int bookingId, CancellationToken cancellationToken = default);
    public Task<HttpResult<BookCourtResult>> BookRecurringCourt(BookRecurringCourtModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> EditRecurringBooking(int bookingId, EditRecurringBookingModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteRecurringBooking(int bookingId, DeleteRecurringBookingModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<RecurringBookingSeriesResult>> GetRecurringBookingSeries(int seriesId, CancellationToken cancellationToken = default);
}
