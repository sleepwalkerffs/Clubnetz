using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Bookings;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Client.Utils;
using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Client.Services.Store.Bookings;

public class BookingStore(IBookingsHttpClient bookingsHttpClient) : SemaphoreStore, IBookingStore
{
    public event Action? OnUpcomingBookingsChanged;
    public event Action? OnBookingsChanged;
    public bool Loaded { get; private set; }
    public List<BookingResult> UpcomingBookings { get; set; } = new();
    public List<BookingResult> Bookings { get; private set; } = [];
    public BookingResult? Booking { get; private set; }

    public Task LoadUpcomingBookings() => RunInLoadingContextAsync(async cancellationToken =>
    {
        Loaded = false;
        var response = await bookingsHttpClient.GetUpcomingBookings(5, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            UpcomingBookings = response.Dto.Bookings.AsLocalTime();
            OnUpcomingBookingsChanged?.Invoke();
            Loaded = true;
        }
    }, nameof(LoadUpcomingBookings));

    public Task LoadBookings(int clubId, DateOnly from, DateOnly to) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await bookingsHttpClient.GetBookings(clubId, from, to, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Bookings = response.Dto.Bookings.AsLocalTime();
            OnBookingsChanged?.Invoke();
        }
    }, nameof(LoadBookings));

    public Task LoadBooking(int bookingId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await bookingsHttpClient.GetBooking(bookingId, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Booking = response.Dto.AsLocalTime();
        }
    }, nameof(LoadBooking));

    public async Task<HttpResult<BookCourtResult>> BookCourt(BookCourtModel model)
    {
        HttpResult<BookCourtResult> response = null!;
        await RunInSavingContextAsync(async cancellationToken =>
        {
            response = await bookingsHttpClient.BookCourt(model, cancellationToken);
            return response;
        }, nameof(BookCourt));
        return response;
    }

    public Task<HttpResult> DeleteBooking(int bookingId) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await bookingsHttpClient.DeleteBooking(bookingId, cancellationToken);
        if (result.Success)
        {
            Booking = null;
        }
        return result;
    }, nameof(DeleteBooking));

    public Task<HttpResult> EditBooking(int bookingId, EditBookingModel model) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await bookingsHttpClient.EditBooking(bookingId, model, cancellationToken);
        return result;
    }, nameof(EditBooking));

    public async Task<HttpResult<BookCourtResult>> BookRecurringCourt(BookRecurringCourtModel model)
    {
        HttpResult<BookCourtResult> response = null!;
        await RunInSavingContextAsync(async cancellationToken =>
        {
            response = await bookingsHttpClient.BookRecurringCourt(model, cancellationToken);
            return response;
        }, nameof(BookRecurringCourt));
        return response;
    }

    public Task<HttpResult> EditRecurringBooking(int bookingId, EditRecurringBookingModel model) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await bookingsHttpClient.EditRecurringBooking(bookingId, model, cancellationToken);
        return result;
    }, nameof(EditRecurringBooking));

    public Task<HttpResult> DeleteRecurringBooking(int bookingId, DeleteRecurringBookingModel model) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await bookingsHttpClient.DeleteRecurringBooking(bookingId, model, cancellationToken);
        if (result.Success)
        {
            Booking = null;
        }
        return result;
    }, nameof(DeleteRecurringBooking));
}