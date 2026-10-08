using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using Bookennis.Shared.Controller.Booking.Shared;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Bookings;

[Authorize(AuthorizationPolicies.Member)]
public class BookingsController(IMediator mediator, IAuthorizationService service, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpPost("BookCourt")]
    public async Task<BookCourtResult> BookCourt([FromBody] BookCourtModel model, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new PlayModeAuthorizationModel(model.PlayModeId), AuthorizationPolicies.CanBookPlayMode).EnsureSucceeded();
        return await mediator.Send(
                   new BookCourt(model.CourtId,
                       new DateTimeOffsetInterval(model.From, model.To),
                       model.TimeZoneInfoId,
                       model.PlayModeId,
                       model.Players,
                       model.Comment,
                       HttpContext.GetRequestTimeZoneOffset(),
                       HttpContext.User.GetId()),
                   cancellationToken);
    }

    [HttpGet]
    public Task<GetBookingsResult> GetBookings(DateOnly dayFrom, DateOnly dayTo, CancellationToken cancellationToken) => mediator.Send(new GetBookings(dayFrom, dayTo), cancellationToken);

    [HttpGet("Upcoming")]
    public Task<GetUpcomingBookingsResult> GetUpcomingBookings(int? amount, CancellationToken cancellationToken) => mediator.Send(new GetUpcomingBookings(amount, userAccessor.GetUserId()), cancellationToken);

    [HttpGet("{id}")]
    public Task<BookingResult> GetBooking(int id, CancellationToken cancellationToken) => mediator.Send(new GetBooking(id), cancellationToken);

    [HttpDelete("{id}")]
    public async Task DeleteBooking(int id, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new BookingEntryAuthorizationModel(id), AuthorizationPolicies.OwnsBooking).EnsureSucceeded();
        await mediator.Send(new DeleteBooking(id), cancellationToken);
    }

    [HttpPut("{id}")]
    public async Task EditBooking(int id, [FromBody] EditBookingModel model, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new BookingEntryAuthorizationModel(id), AuthorizationPolicies.CanEditBooking).EnsureSucceeded();
        await service.AuthorizeAsync(HttpContext.User, new PlayModeAuthorizationModel(model.PlayModeId), AuthorizationPolicies.CanBookPlayMode).EnsureSucceeded();
        await mediator.Send(
            new EditBooking(id,
                model.CourtId,
                new DateTimeOffsetInterval(model.From, model.To),
                model.TimeZoneInfoId,
                model.PlayModeId,
                model.Players,
                model.Comment,
                HttpContext.GetRequestTimeZoneOffset(),
                HttpContext.User.GetId()),
            cancellationToken);
    }

    [HttpPost("BookRecurring")]
    public async Task<BookCourtResult> BookRecurringCourt([FromBody] BookRecurringCourtModel model, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new PlayModeAuthorizationModel(model.PlayModeId), AuthorizationPolicies.CanBookPlayMode).EnsureSucceeded();
        return await mediator.Send(
            new BookRecurringCourt(
                model.CourtId,
                new DateTimeOffsetInterval(model.From, model.To),
                model.TimeZoneInfoId,
                model.PlayModeId,
                model.Players,
                model.RecurrenceIntervalWeeks,
                model.EndDate,
                model.Comment,
                HttpContext.GetRequestTimeZoneOffset(),
                HttpContext.User.GetId()),
            cancellationToken);
    }

    [HttpPut("Recurring/{id}")]
    public async Task EditRecurringBooking(int id, [FromBody] EditRecurringBookingModel model, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new BookingEntryAuthorizationModel(id), AuthorizationPolicies.CanEditBooking).EnsureSucceeded();
        await service.AuthorizeAsync(HttpContext.User, new PlayModeAuthorizationModel(model.PlayModeId), AuthorizationPolicies.CanBookPlayMode).EnsureSucceeded();
        await mediator.Send(
            new EditRecurringBooking(
                id,
                model.CourtId,
                new DateTimeOffsetInterval(model.From, model.To),
                model.TimeZoneInfoId,
                model.PlayModeId,
                model.Players,
                model.Scope,
                model.RecurrenceIntervalWeeks,
                model.EndDate,
                model.Comment,
                HttpContext.GetRequestTimeZoneOffset(),
                HttpContext.User.GetId()),
            cancellationToken);
    }

    [HttpDelete("Recurring/{id}")]
    public async Task DeleteRecurringBooking(int id, [FromBody] DeleteRecurringBookingModel model, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new BookingEntryAuthorizationModel(id), AuthorizationPolicies.OwnsBooking).EnsureSucceeded();
        await mediator.Send(new DeleteRecurringBooking(id, model.Scope), cancellationToken);
    }

    [HttpGet("RecurringSeries/{seriesId}")]
    public Task<RecurringBookingSeriesResult> GetRecurringBookingSeries(int seriesId, CancellationToken cancellationToken)
        => mediator.Send(new GetRecurringBookingSeries(seriesId), cancellationToken);
}