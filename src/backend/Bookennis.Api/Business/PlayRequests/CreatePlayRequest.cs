using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.PlayRequests;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.PlayRequests;

/// <summary>
/// A member looks for players. Nothing is blocked. The request is only created when the requester could book the slot themself
/// (a request that is doomed from the start would only waste the time of the players who were asked).
/// </summary>
public record CreatePlayRequest(
    int ClubId,
    int UserId,
    int PlayModeId,
    DateTimeOffsetInterval Interval,
    string TimeZoneInfoId,
    List<int> CourtIds,
    int? PlayersNeeded,
    List<int> InviteeMemberIds,
    string? Comment,
    DateTimeOffset? ExpiresAt) : ICommand<int>
{
    public class Handler(AppDbContext context, IBookingDomainService bookingService, IPlayRequestPushNotifier notifier) : IRequestHandler<CreatePlayRequest, int>
    {
        public async Task<int> Handle(CreatePlayRequest request, CancellationToken cancellationToken)
        {
            var requesterId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            var playMode = await context.PlayModes.SingleRequiredAsync(p => p.Id == request.PlayModeId && p.ClubId == request.ClubId, cancellationToken);
            var requesterRoles = await context.ClubMembers.Where(m => m.Id == requesterId).Select(m => m.UserRoles).SingleAsync(cancellationToken);
            if (!PlayRequestBooking.MayBook(playMode, requesterRoles))
                throw new PreconditionException(PlayRequest.ErrorCode.PlayRequestPlayModeNotAllowed, "This play mode can't be booked by the requester");

            var playersNeeded = playMode.FixedPlayerCount ?? request.PlayersNeeded ?? 2;

            // The requester and everybody they ask are active members of the club (who else could play)
            var askedIds = request.InviteeMemberIds.Distinct().ToList();
            var activeIds = await ActiveMembership.ActiveMemberIds(context, request.ClubId, [requesterId, .. askedIds], cancellationToken);
            activeIds.IntersectWith(await PlayRequestBooking.WithOwnLogin(context, [.. activeIds], cancellationToken));
            if (!activeIds.Contains(requesterId) || askedIds.Any(id => id != requesterId && !activeIds.Contains(id)))
                throw new PreconditionException(PlayRequest.ErrorCode.PlayRequestMemberNotActive, "Only active members of the club can play together");

            var playRequest = PlayRequest.Create(
                request.ClubId,
                requesterId,
                new PlayRequest.RequestData(
                    request.PlayModeId,
                    request.Interval,
                    request.TimeZoneInfoId,
                    request.CourtIds,
                    playersNeeded,
                    askedIds,
                    request.Comment,
                    request.ExpiresAt),
                now);

            // The booking rules of the requester: opening hours, quotas, enrollment, blockings, ... The booking itself is never saved here
            var builder = await BookingContextBuilder.Load(
                context, request.PlayModeId, playRequest.CourtIds, [requesterId], request.UserId, [request.Interval], cancellationToken);
            await PlayRequestBooking.BookFirstFreeCourt(
                builder, bookingService, playRequest.CourtIds, request.Interval, request.TimeZoneInfoId,
                playersNeeded, validateAsIfFull: true, cancellationToken);

            context.Add(playRequest);
            await context.SaveChangesAsync(cancellationToken);

            await notifier.RequestCreated(playRequest.Id, cancellationToken);
            return playRequest.Id;
        }
    }
}
