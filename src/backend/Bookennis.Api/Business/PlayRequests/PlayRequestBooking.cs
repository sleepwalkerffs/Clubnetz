using Bookennis.Domain.PlayRequests;
using Bookennis.Api.Business.Members;
using Bookennis.Domain.Courts;
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
        nameof(BookingDomainService.ErrorCode.PlayModeNotAllowedOnCourt),
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
        int playersNeeded,
        bool validateAsIfFull,
        CancellationToken cancellationToken)
    {
        PreconditionException? lastError = null;
        foreach (var courtId in courtIds)
        {
            var context = await builder.BuildContext(courtId, interval, [], cancellationToken);
            if (validateAsIfFull && context.Participants.Count < playersNeeded)
                context = context with { Participants = [.. context.Participants, .. Enumerable.Repeat(context.Participants[^1], playersNeeded - context.Participants.Count)] };

            try
            {
                return bookingService.BookCourt(context, interval, builder.Region.TimeZoneId, comment: null);
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
    /// The sport of the courts of a request; all courts must be for the same sport (<see cref="PlayRequest.ErrorCode.PlayRequestCourtsOfDifferentSports"/>).
    /// Courts of other clubs or that don't exist are left to the request (it reports them).
    /// </summary>
    public static async Task<Sport> SportOfCourts(Data.AppDbContext context, int clubId, IReadOnlyCollection<int> courtIds, CancellationToken cancellationToken)
    {
        var sports = await context.Courts
            .Where(c => c.ClubId == clubId && courtIds.Contains(c.Id))
            .Select(c => c.Sport)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sports.Count > 1)
            throw new PreconditionException(PlayRequest.ErrorCode.PlayRequestCourtsOfDifferentSports, "The courts of a request must be for the same sport");

        return sports.Count == 1 ? sports[0] : Sport.Tennis;
    }

    /// <summary>
    /// The members among <paramref name="memberIds"/> (every member when <c>null</c>) who are enrolled in the active season, but for other sports
    /// than <paramref name="sport"/>: they can't play on these courts. Members who are not enrolled at all are not in the list (the booking rules decide about them, as before).
    /// </summary>
    public static async Task<HashSet<int>> EnrolledForOtherSports(Data.AppDbContext context, int clubId, IReadOnlyCollection<int>? memberIds, Sport sport, CancellationToken cancellationToken)
    {
        var season = await context.GetActiveSeason(clubId, cancellationToken);
        if (season is null)
            return [];

        var enrollments = await context.MemberSeasons
            .Where(ms => ms.SeasonId == season.Id && (memberIds == null || memberIds.Contains(ms.MemberId)))
            .Select(ms => new { ms.MemberId, ms.Sports })
            .ToListAsync(cancellationToken);

        return enrollments.Where(e => !Sports.Covers(e.Sports, sport)).Select(e => e.MemberId).ToHashSet();
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
