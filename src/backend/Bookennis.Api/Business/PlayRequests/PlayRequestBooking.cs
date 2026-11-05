using Bookennis.Api.Business.Bookings;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.PlayRequests;

/// <summary>
/// Books the slot of a play request through the normal booking rules (<see cref="IBookingDomainService.BookCourt"/>), so a play request
/// can never do what a booking could not. Courts are tried in the order of the request.
/// </summary>
internal static class PlayRequestBooking
{
    /// <summary>A reason to try the next court. Every other broken rule (quotas, enrollment, opening hours, ...) is the same on every court.</summary>
    private static readonly string[] CourtErrors =
    [
        nameof(BookingDomainService.ErrorCode.CourtOccupied),
        nameof(BookingDomainService.ErrorCode.CourtBlocked),
        nameof(BookingDomainService.ErrorCode.CourtInactive),
    ];

    /// <summary>
    /// The booking (not added to the context yet) on the first court that is free; throws the error of the last court when none is.
    /// <paramref name="validateAsIfFull"/>: only the requester is loaded (creating the request); the rules are checked as if the
    /// requester were as many players as the play mode needs, so everything but the missing players is verified.
    /// </summary>
    public static async Task<Booking> BookFirstFreeCourt(
        BookingContextBuilder builder,
        IBookingDomainService bookingService,
        IReadOnlyList<int> courtIds,
        DateTimeOffsetInterval interval,
        string timeZoneInfoId,
        int? clientTimeZoneOffset,
        int playersNeeded,
        bool validateAsIfFull,
        CancellationToken cancellationToken)
    {
        PreconditionException? lastError = null;
        foreach (var courtId in courtIds)
        {
            var context = await builder.BuildContext(courtId, interval, clientTimeZoneOffset, [], cancellationToken);
            if (validateAsIfFull && context.Participants.Count < playersNeeded)
                context = context with { Participants = [.. context.Participants, .. Enumerable.Repeat(context.Participants[^1], playersNeeded - context.Participants.Count)] };

            try
            {
                return bookingService.BookCourt(context, interval, timeZoneInfoId, comment: null);
            }
            catch (PreconditionException e) when (e.ErrorCode is not null && CourtErrors.Contains(e.ErrorCode))
            {
                lastError = e;
            }
        }

        if (lastError is null)
            throw new InvalidOperationException("A play request has at least one court");

        throw lastError;
    }

    /// <summary>
    /// The members among <paramref name="memberIds"/> who have a login of their own. Children without one are answered for by a parent,
    /// which play requests do not support (the push would reach the parent, who could not answer), so they are never asked.
    /// </summary>
    public static async Task<HashSet<int>> WithOwnLogin(Data.AppDbContext context, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
        => (await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where memberIds.Contains(member.Id) && user.BelongsToUserId == null
            select member.Id
        ).ToListAsync(cancellationToken)).ToHashSet();

    /// <summary>The play mode may be booked by the member (<see cref="PlayMode.AllowedRoles"/>, admins always).</summary>
    public static bool MayBook(PlayMode playMode, MemberRole[] memberRoles)
        => memberRoles.Contains(MemberRole.Admin) || playMode.AllowedRoles.Any(memberRoles.Contains);
}
