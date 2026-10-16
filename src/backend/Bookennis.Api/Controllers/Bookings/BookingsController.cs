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

    /// <summary>
    /// Books several slots (court and time) at once with the same play mode and players, as the multi-day planner does.
    /// Every slot follows the rules of a single booking; nothing is saved unless every slot is valid.
    /// With <c>dryRun</c> nothing is saved and the result tells which slots would fail and which bookings an overbooking play mode would remove.
    /// </summary>
    [HttpPost("Batch")]
    public async Task<BookCourtsResult> BookCourts([FromBody] BookCourtsModel model, CancellationToken cancellationToken)
    {
        await service.AuthorizeAsync(HttpContext.User, new PlayModeAuthorizationModel(model.PlayModeId), AuthorizationPolicies.CanBookPlayMode).EnsureSucceeded();
        return await mediator.Send(
            new BookCourts(
                model.Slots,
                model.TimeZoneInfoId,
                model.PlayModeId,
                model.Players,
                model.Comment,
                model.DryRun,
                HttpContext.GetRequestTimeZoneOffset(),
                HttpContext.User.GetId(),
                model.ExpectedBookingIdsToRemove),
            cancellationToken);
    }

    /// <summary>The bookings of the calendar. Members who are not active only see that a court is booked, except for their own bookings.</summary>
    [HttpGet]
    public async Task<GetBookingsResult> GetBookings(DateOnly dayFrom, DateOnly dayTo, CancellationToken cancellationToken)
        => await mediator.Send(new GetBookings(dayFrom, dayTo, await AnonymousViewer()), cancellationToken);

    [HttpGet("Upcoming")]
    public Task<GetUpcomingBookingsResult> GetUpcomingBookings(int? amount, CancellationToken cancellationToken) => mediator.Send(new GetUpcomingBookings(amount, userAccessor.GetUserId()), cancellationToken);

    [HttpGet("{id}")]
    public async Task<BookingResult> GetBooking(int id, CancellationToken cancellationToken)
        => await mediator.Send(new GetBooking(id, await AnonymousViewer()), cancellationToken);

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
    [Authorize(AuthorizationPolicies.ActiveMember)]
    public Task<RecurringBookingSeriesResult> GetRecurringBookingSeries(int seriesId, CancellationToken cancellationToken)
        => mediator.Send(new GetRecurringBookingSeries(seriesId), cancellationToken);

    /// <summary>The user to anonymize the bookings for: <c>null</c> for active members (they see everything).</summary>
    private async Task<int?> AnonymousViewer()
        => await service.IsActiveMember(HttpContext.User) ? null : userAccessor.GetUserId();
}
