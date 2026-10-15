using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Client.Services.Store.Bookings;

public interface IBookingStore : ISemaphoreStore
{
    public event Action OnUpcomingBookingsChanged;
    public event Action OnBookingsChanged;
    public List<BookingResult> UpcomingBookings { get; }
    public List<BookingResult> Bookings { get; }
    public BookingResult? Booking { get; }
    public Task LoadUpcomingBookings();
    public Task LoadBookings(int clubId, DateOnly from, DateOnly to);
    public Task LoadBooking(int bookingId);
    public Task<HttpResult<BookCourtResult>> BookCourt(BookCourtModel model);
    /// <summary>
    /// Books several slots at once (multi-day planner). With <see cref="BookCourtsModel.DryRun"/> nothing is saved and every slot is checked;
    /// dry runs use their own context, so a newer check cancels an older one but never a real save.
    /// </summary>
    public Task<HttpResult<BookCourtsResult>> BookCourts(BookCourtsModel model);
    public Task<HttpResult> EditBooking(int bookingId, EditBookingModel model);
    public Task<HttpResult> DeleteBooking(int bookingId);
    public Task<HttpResult<BookCourtResult>> BookRecurringCourt(BookRecurringCourtModel model);
    public Task<HttpResult> EditRecurringBooking(int bookingId, EditRecurringBookingModel model);
    public Task<HttpResult> DeleteRecurringBooking(int bookingId, DeleteRecurringBookingModel model);
}