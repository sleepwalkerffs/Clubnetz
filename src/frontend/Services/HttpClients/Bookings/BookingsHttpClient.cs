using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;
using Microsoft.AspNetCore.WebUtilities;

namespace Bookennis.Client.Services.HttpClients.Bookings;

public class BookingsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IBookingsHttpClient
{
    public async Task<HttpResult<GetUpcomingBookingsResult>> GetUpcomingBookings(int amount, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("Upcoming", cancellationToken)).AsHttpResult<GetUpcomingBookingsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<BookCourtResult>> BookCourt(BookCourtModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("BookCourt", model, jsonOptions, cancellationToken)).AsHttpResult<BookCourtResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<BookingResult>> GetBooking(int bookingId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"{bookingId}", cancellationToken)).AsHttpResult<BookingResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetBookingsResult>> GetBookings(int clubId, DateOnly dayFrom, DateOnly dayTo, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            { "dayFrom", dayFrom.ToString(CultureInfo.InvariantCulture) },
            { "dayTo", dayTo.ToString(CultureInfo.InvariantCulture) },
        };

        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString(httpClient.BaseAddress!.AbsoluteUri, query!), cancellationToken)).AsHttpResult<GetBookingsResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> DeleteBooking(int bookingId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"{bookingId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> EditBooking(int bookingId, EditBookingModel model, CancellationToken cancellationToken)
        => await (await httpClient.PutAsJsonAsync($"{bookingId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<BookCourtResult>> BookRecurringCourt(BookRecurringCourtModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("BookRecurring", model, jsonOptions, cancellationToken)).AsHttpResult<BookCourtResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> EditRecurringBooking(int bookingId, EditRecurringBookingModel model, CancellationToken cancellationToken)
        => await (await httpClient.PutAsJsonAsync($"Recurring/{bookingId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteRecurringBooking(int bookingId, DeleteRecurringBookingModel model, CancellationToken cancellationToken)
        => await (await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"Recurring/{bookingId}") { Content = JsonContent.Create(model, options: jsonOptions) }, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<RecurringBookingSeriesResult>> GetRecurringBookingSeries(int seriesId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"RecurringSeries/{seriesId}", cancellationToken)).AsHttpResult<RecurringBookingSeriesResult>(jsonOptions, cancellationToken);
}