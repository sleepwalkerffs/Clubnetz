using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

/// <summary>
/// Loads what <see cref="BookingDomainService"/> needs to validate new bookings of one play mode with the same players,
/// and builds the domain context per booked slot. <see cref="BookCourt"/> (one booking) and <see cref="BookCourts"/> (the planner)
/// both use it, so a batch can never be validated with other rules than a single booking.
/// </summary>
internal sealed class BookingContextBuilder
{
    private readonly AppDbContext context;
    private readonly int excludedBookingId;
    private readonly List<ParticipantInfo> participants;
    private readonly HashSet<int> enrolledMemberIds;
    private readonly ILookup<int, PlayerBooking> chargingBookingsByMember;

    private BookingContextBuilder(
        AppDbContext context,
        Club club,
        PlayMode playMode,
        IReadOnlyDictionary<int, Court> courts,
        List<ParticipantInfo> participants,
        Season? activeSeason,
        HashSet<int> enrolledMemberIds,
        List<PlayerBooking> chargingBookings,
        int excludedBookingId)
    {
        this.context = context;
        this.excludedBookingId = excludedBookingId;
        Club = club;
        PlayMode = playMode;
        Courts = courts;
        this.participants = participants;
        ActiveSeason = activeSeason;
        this.enrolledMemberIds = enrolledMemberIds;
        chargingBookingsByMember = chargingBookings.ToLookup(b => b.MemberId);
    }

    public Club Club { get; }
    public PlayMode PlayMode { get; }
    public IReadOnlyDictionary<int, Court> Courts { get; }
    public Season? ActiveSeason { get; }

    /// <summary>
    /// Loads the club, the play mode, the courts and the players, and checks that the user may book for these players.
    /// <c>intervals</c> are all times that will be booked; the bookings of the players around them are loaded once for all of them.
    /// <c>editedBookingId</c> is the booking that is moved or changed (<see cref="EditBooking"/>, <see cref="EditRecurringBooking"/>):
    /// it is left out of every count and of the intersecting bookings, because it is replaced by the new times.
    /// </summary>
    /// <exception cref="EntityNotFoundException">The play mode, a court or a player does not exist in the club.</exception>
    /// <exception cref="PreconditionException"><see cref="BookCourt.ErrorCode.UserNotAllowedToBook"/></exception>
    public static async Task<BookingContextBuilder> Load(
        AppDbContext context,
        int playModeId,
        IReadOnlyCollection<int> courtIds,
        List<int> players,
        int userId,
        IReadOnlyCollection<DateTimeOffsetInterval> intervals,
        CancellationToken cancellationToken,
        int? editedBookingId = null)
    {
        // Ids start at 1, so 0 excludes nothing
        var excludedBookingId = editedBookingId ?? 0;

        var clubAndPlayMode = await (
            from currentClub in context.Clubs
            join playMode in context.PlayModes on currentClub.Id equals playMode.ClubId
            where playMode.Id == playModeId
            select new { Club = currentClub, PlayMode = playMode }
        )
            .AsNoTracking()
            .SingleRequiredAsync(cancellationToken);

        var club = clubAndPlayMode.Club;

        var distinctCourtIds = courtIds.Distinct().ToList();
        var courts = await context.Courts
            .Where(c => c.ClubId == club.Id && distinctCourtIds.Contains(c.Id))
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        if (courts.Count != distinctCourtIds.Count)
            throw new EntityNotFoundException(typeof(Court));

        var participants = await LoadParticipants(context, club, playModeId, players, userId, excludedBookingId, cancellationToken);

        if (participants.Count != players.Count)
            throw new EntityNotFoundException("Player does not exist");

        if (!participants.Any(i => i.IsAllowedUser))
            throw new PreconditionException(BookCourt.ErrorCode.UserNotAllowedToBook, "User is not allowed to make booking for the provided memberIds");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeSeason = await context.Seasons
            .Where(s => s.ClubId == club.Id && s.Period.From <= today && today <= s.Period.To)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var enrolledMemberIds = activeSeason is not null
            ? await context.MemberSeasons
                .Where(ms => ms.SeasonId == activeSeason.Id && players.Contains(ms.MemberId))
                .Select(ms => ms.MemberId)
                .ToListAsync(cancellationToken)
            : [];

        var chargingBookings = await LoadChargingBookings(context, participants, intervals, excludedBookingId, cancellationToken);

        return new BookingContextBuilder(context, club, clubAndPlayMode.PlayMode, courts, participants, activeSeason, enrolledMemberIds.ToHashSet(), chargingBookings, excludedBookingId);
    }

    /// <summary>
    /// The domain context for booking <c>interval</c> on a court.
    /// <c>pendingBookings</c> are the times of bookings with the same play mode and players that are not saved yet but will be saved
    /// together with this one (earlier slots of the same batch). They count like existing bookings.
    /// </summary>
    public async Task<BookingDomainService.Context> BuildContext(
        int courtId,
        DateTimeOffsetInterval interval,
        int? clientTimeZoneOffset,
        IReadOnlyCollection<DateTimeOffsetInterval> pendingBookings,
        CancellationToken cancellationToken)
    {
        var court = Courts[courtId];

        var intersectingBookings = await context
            .Bookings.Where(b => b.CourtId == courtId && b.Interval.Intersects(interval) && b.Id != excludedBookingId)
            .AsNoTracking()
            .ExpandProjectables()
            .ToListAsync(cancellationToken);

        var courtBlockingTitles = await context.GetCourtBlockingTitles(courtId, interval, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // The pending bookings count where an existing booking of this play mode would count
        var pendingCharging = PlayMode.IsChargingBookingSubscription ? pendingBookings : [];
        var pendingUpcoming = pendingCharging.Count(b => b.To > now);
        var pendingActive = pendingCharging.Count(b => b.Intersects(interval));
        var pendingOnThatDay = pendingCharging.Count(b => DateOfEnd(b.To) == interval.From.Date);
        var pendingInSeason = PlayMode.MaxBookingsPerSeason is not null ? pendingBookings.Count(IsInActiveSeason) : 0;

        return new BookingDomainService.Context
        {
            ClubId = Club.Id,
            Court = court,
            OpeningHours = Club.OpeningHours,
            PrimeTimeSettings = Club.PrimeTimeSettings,
            BookingMemberRole = [MemberRole.User],
            Participants = participants.ConvertAll(i =>
            {
                var chargingBookings = chargingBookingsByMember[i.Member.Id].ToList();
                return new BookingDomainService.Context.PlayerContext
                {
                    Member = i.Member,
                    FullName = i.FullName,
                    IsChild = i.IsChild,
                    UpcomingBookingsCount = i.UpcomingBookingsCount + pendingUpcoming,
                    ActiveBookingsAtTimeOfReservation = chargingBookings.Count(b => b.Interval.Intersects(interval)) + pendingActive,
                    BookingsOnThatDay = chargingBookings.Count(b => b.EndDate == interval.From.Date) + pendingOnThatDay,
                    IsAllowedInCurrentSeason = enrolledMemberIds.Contains(i.Member.Id),
                    IsAtpParticipant = Club.IsAtpClub && i.IsFromAtpClub && i.Member.MemberType == MemberType.ClubMember,
                    PlayModeBookingsInSeasonCount = i.PlayModeBookingsInSeasonCount + pendingInSeason,
                };
            }),
            IntersectingBookings = intersectingBookings,
            PlayMode = PlayMode,
            ClientTimeZoneOffset = clientTimeZoneOffset,
            BookingGracePeriodInMinutes = Club.BookingGracePeriodInMinutes,
            ActiveSeasonId = ActiveSeason?.Id,
            CourtBlockingTitles = courtBlockingTitles,
        };
    }

    /// <summary>
    /// The date of the end of a booking as the database query of <see cref="LoadChargingBookings"/> calculates it
    /// (Npgsql takes the date of a <c>timestamptz</c> in UTC).
    /// </summary>
    private static DateTime DateOfEnd(DateTimeOffset to) => to.UtcDateTime.Date;

    /// <summary>Same rule as the season count of the participant query.</summary>
    private bool IsInActiveSeason(DateTimeOffsetInterval interval)
        => ActiveSeason is not null
            && DateOnly.FromDateTime(interval.From.UtcDateTime) >= ActiveSeason.Period.From
            && DateOnly.FromDateTime(interval.To.UtcDateTime) <= ActiveSeason.Period.To;

    private static async Task<List<ParticipantInfo>> LoadParticipants(AppDbContext context, Club currentClub, int playModeId, List<int> players, int userId, int excludedBookingId, CancellationToken cancellationToken)
    {
        var childAgeThreshold = currentClub.PrimeTimeSettings.ChildAgeThreshold;
        var clubId = currentClub.Id;

        var localParticipants = await (
            from member in context.Set<Member>()
            join user in context.Users on member.UserId equals user.Id

            let isChild = user.Birthday.AddYears(childAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

            let upcomingBookingsCount = (
                from bookingPlayer in context.BookingPlayers
                join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                    && bookingPlayer.Booking.Id != excludedBookingId
                select bookingPlayer.Id
            ).Count()

            let playModeBookingsInSeasonCount = (
                from bookingPlayer in context.BookingPlayers
                where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == playModeId
                    && bookingPlayer.Booking.Id != excludedBookingId
                join season in context.Seasons on clubId equals season.ClubId
                let today = DateOnly.FromDateTime(DateTime.UtcNow)
                where season.Period.From <= today && today <= season.Period.To
                where DateOnly.FromDateTime(bookingPlayer.Booking.Interval.From.DateTime) >= season.Period.From
                    && DateOnly.FromDateTime(bookingPlayer.Booking.Interval.To.DateTime) <= season.Period.To
                select bookingPlayer.Id
            ).Count()

            let isAllowedUser = user.Id == userId
                || (from familyMember in context.FamilyMembers join member in context.ClubMembers on familyMember.MemberId equals member.Id select member.UserId).Any(familyUserId =>
                    familyUserId == userId
                )

            where players.Contains(member.Id)
            select new ParticipantInfo
            {
                IsAllowedUser = isAllowedUser,
                Member = member,
                FullName = user.FullName,
                IsChild = isChild,
                UpcomingBookingsCount = upcomingBookingsCount,
                PlayModeBookingsInSeasonCount = playModeBookingsInSeasonCount,
                IsFromAtpClub = false,
            }
        )
            .AsSingleQuery()
            .AsNoTracking()
            .ExpandProjectables()
            .ToListAsync(cancellationToken);

        var participants = new List<ParticipantInfo>(localParticipants);

        // If the current club is an ATP club, fetch cross-club ATP members that weren't found locally
        var missingPlayerIds = players.Except(localParticipants.Select(p => p.Member.Id)).ToList();
        if (missingPlayerIds.Count > 0 && currentClub.IsAtpClub)
        {
            var atpParticipants = await (
                from member in context.ClubMembers.IgnoreQueryFilters()
                join user in context.Users on member.UserId equals user.Id
                join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id

                let isChild = user.Birthday.AddYears(childAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

                let upcomingBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                    && bookingPlayer.Booking.Id != excludedBookingId
                    select bookingPlayer.Id
                ).Count()

                let playModeBookingsInSeasonCount = (
                    from bookingPlayer in context.BookingPlayers
                    where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == playModeId
                    && bookingPlayer.Booking.Id != excludedBookingId
                    join season in context.Seasons on clubId equals season.ClubId
                    let today = DateOnly.FromDateTime(DateTime.UtcNow)
                    where season.Period.From <= today && today <= season.Period.To
                    where DateOnly.FromDateTime(bookingPlayer.Booking.Interval.From.DateTime) >= season.Period.From
                        && DateOnly.FromDateTime(bookingPlayer.Booking.Interval.To.DateTime) <= season.Period.To
                    select bookingPlayer.Id
                ).Count()

                where missingPlayerIds.Contains(member.Id)
                    && club.IsAtpClub
                    && club.Id != clubId
                select new ParticipantInfo
                {
                    IsAllowedUser = true,
                    Member = member,
                    FullName = user.FullName,
                    IsChild = isChild,
                    UpcomingBookingsCount = upcomingBookingsCount,
                    PlayModeBookingsInSeasonCount = playModeBookingsInSeasonCount,
                    IsFromAtpClub = true,
                }
            )
                .AsSingleQuery()
                .AsNoTracking()
                .ExpandProjectables()
                .ToListAsync(cancellationToken);

            participants.AddRange(atpParticipants);
        }

        return participants;
    }

    /// <summary>
    /// The bookings of the players in play modes that charge the booking subscription, around the booked times.
    /// They are counted per booked slot in memory (bookings at the same time, bookings on the same day).
    /// Like the participant queries, players of the own club only count bookings of the own club and ATP players their bookings in every club.
    /// </summary>
    private static async Task<List<PlayerBooking>> LoadChargingBookings(
        AppDbContext context,
        List<ParticipantInfo> participants,
        IReadOnlyCollection<DateTimeOffsetInterval> intervals,
        int excludedBookingId,
        CancellationToken cancellationToken)
    {
        if (intervals.Count == 0 || participants.Count == 0)
            return [];

        // Wide enough for bookings that intersect a slot and for bookings that end on the date of a slot in any time zone
        var windowFrom = intervals.Min(i => i.From).AddDays(-2);
        var windowTo = intervals.Max(i => i.To).AddDays(2);

        var localIds = participants.Where(p => !p.IsFromAtpClub).Select(p => p.Member.Id).ToList();
        var atpIds = participants.Where(p => p.IsFromAtpClub).Select(p => p.Member.Id).ToList();

        var result = new List<PlayerBooking>();
        if (localIds.Count > 0)
            result.AddRange(await Query(context.BookingPlayers, context.PlayModes, localIds).ToListAsync(cancellationToken));

        if (atpIds.Count > 0)
            result.AddRange(await Query(context.BookingPlayers.IgnoreQueryFilters(), context.PlayModes.IgnoreQueryFilters(), atpIds).ToListAsync(cancellationToken));

        return result;

        IQueryable<PlayerBooking> Query(IQueryable<BookingPlayer> bookingPlayers, IQueryable<PlayMode> playModes, List<int> memberIds)
            => from bookingPlayer in bookingPlayers
               join playMode in playModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
               where playMode.IsChargingBookingSubscription
                   && memberIds.Contains(bookingPlayer.MemberId)
                   && bookingPlayer.Booking.Id != excludedBookingId
                   && bookingPlayer.Booking.Interval.To >= windowFrom
                   && bookingPlayer.Booking.Interval.From <= windowTo
               select new PlayerBooking(
                   bookingPlayer.MemberId,
                   bookingPlayer.Booking.Interval.From,
                   bookingPlayer.Booking.Interval.To,
                   bookingPlayer.Booking.Interval.To.Date);
    }

    private sealed record ParticipantInfo
    {
        public required bool IsAllowedUser { get; init; }
        public required Member Member { get; init; }
        public required string FullName { get; init; }
        public required bool IsChild { get; init; }
        public required int UpcomingBookingsCount { get; init; }
        public required int PlayModeBookingsInSeasonCount { get; init; }
        public required bool IsFromAtpClub { get; init; }
    }

    /// <summary>A booking of a player; <c>EndDate</c> is the date of its end as the database calculates it.</summary>
    private sealed record PlayerBooking(int MemberId, DateTimeOffset From, DateTimeOffset To, DateTime EndDate)
    {
        public DateTimeOffsetInterval Interval => new(From, To.ToOffset(From.Offset));
    }
}
