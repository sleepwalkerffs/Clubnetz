using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.PlayRequests;
using Bookennis.Global.Intervals;
using Bookennis.Domain.Bookings;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.PlayRequests;

/// <summary><see cref="BookingId"/> is set when this answer filled the last spot and the court was booked.</summary>
public record AcceptPlayRequestResult(bool IsFilled, int? BookingId);

/// <summary>
/// An invitee accepts. When the last spot is taken the court is booked for the requester and everybody who accepted - the first free court of
/// the request, through the normal booking rules. If the booking fails (court taken, quota used up, ...) the error is returned, nothing is
/// saved and the request stays open as it was.
/// </summary>
public record AcceptPlayRequest(int ClubId, int UserId, int PlayRequestId) : ICommand<AcceptPlayRequestResult>
{
    public class Handler(AppDbContext context, IBookingDomainService bookingService, IPlayRequestPushNotifier notifier) : IRequestHandler<AcceptPlayRequest, AcceptPlayRequestResult>
    {
        public async Task<AcceptPlayRequestResult> Handle(AcceptPlayRequest request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            // Two people who accept the last two spots at the same time must see each other: one at a time per club, like every booking
            await context.LockCourts(request.ClubId, cancellationToken);

            var playRequest = await context.PlayRequests
                .Include(r => r.Invitees)
                .SingleRequiredAsync(r => r.Id == request.PlayRequestId && r.ClubId == request.ClubId, cancellationToken);

            var playMode = await context.PlayModes.SingleRequiredAsync(p => p.Id == playRequest.PlayModeId && p.ClubId == request.ClubId, cancellationToken);
            var roles = await context.ClubMembers.Where(m => m.Id == memberId).Select(m => m.UserRoles).SingleAsync(cancellationToken);
            if (!PlayRequestBooking.MayBook(playMode, roles))
                throw new PreconditionException(PlayRequest.ErrorCode.PlayRequestPlayModeNotAllowed, "This play mode can't be booked by you");

            if (!await ActiveMembership.IsActive(context, request.ClubId, memberId, cancellationToken))
                throw new PreconditionException(PlayRequest.ErrorCode.PlayRequestMemberNotActive, "Only active members of the club can play together");

            var isFull = playRequest.Accept(memberId, now);
            var interval = new DateTimeOffsetInterval(playRequest.StartsAt, playRequest.EndsAt);
            if (!isFull)
            {
                // The acceptor has to be bookable together with the others, otherwise their yes would block the last spot for everybody
                var partial = await BookingContextBuilder.Load(
                    context, playRequest.PlayModeId, playRequest.CourtIds, playRequest.PlayerMemberIds(), request.UserId, [interval], cancellationToken);
                await PlayRequestBooking.BookFirstFreeCourt(
                    partial, bookingService, playRequest.CourtIds, interval, playRequest.TimeZoneInfoId,
                    playRequest.PlayersNeeded, validateAsIfFull: true, cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                await notifier.Accepted(playRequest.Id, memberId, cancellationToken);
                return new AcceptPlayRequestResult(false, null);
            }

            var builder = await BookingContextBuilder.Load(
                context, playRequest.PlayModeId, playRequest.CourtIds, playRequest.PlayerMemberIds(), request.UserId, [interval], cancellationToken);

            var booking = context.Add(await PlayRequestBooking.BookFirstFreeCourt(
                builder, bookingService, playRequest.CourtIds, interval, playRequest.TimeZoneInfoId,
                playRequest.PlayersNeeded, validateAsIfFull: false, cancellationToken)).Entity;
            await context.SaveChangesAsync(cancellationToken);

            playRequest.MarkFilled(booking.Id, now);
            await context.SaveChangesAsync(cancellationToken);

            // The players hear about the booking from the booking itself (BookingAdded)
            return new AcceptPlayRequestResult(true, booking.Id);
        }
    }
}

/// <summary>An invitee says no (or takes back a yes while the request is open). The others can still accept.</summary>
public record DeclinePlayRequest(int ClubId, int UserId, int PlayRequestId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeclinePlayRequest>
    {
        public async Task<Unit> Handle(DeclinePlayRequest request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);

            await context.LockCourts(request.ClubId, cancellationToken);
            var playRequest = await context.PlayRequests
                .Include(r => r.Invitees)
                .SingleRequiredAsync(r => r.Id == request.PlayRequestId && r.ClubId == request.ClubId, cancellationToken);

            playRequest.Decline(memberId, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>The requester withdraws an open request. The members who were asked and have not declined get a push notification.</summary>
public record CancelPlayRequest(int ClubId, int UserId, int PlayRequestId) : ICommand
{
    public class Handler(AppDbContext context, IPlayRequestPushNotifier notifier) : IRequestHandler<CancelPlayRequest>
    {
        public async Task<Unit> Handle(CancelPlayRequest request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);

            await context.LockCourts(request.ClubId, cancellationToken);
            var playRequest = await context.PlayRequests
                .Include(r => r.Invitees)
                .SingleRequiredAsync(r => r.Id == request.PlayRequestId && r.ClubId == request.ClubId, cancellationToken);

            playRequest.Cancel(memberId, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(cancellationToken);

            await notifier.Cancelled(playRequest.Id, cancellationToken);
            return default;
        }
    }
}
