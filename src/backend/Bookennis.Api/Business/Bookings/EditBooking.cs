using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record EditBooking(
    [Required] int BookingEntryId,
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    int? ClientTimeZoneOffset,
    int UserId
) : ICommand
{
    public enum ErrorCode
    {
        CannotEditBookingFromThePast = 0,
        UserNotAllowedToBook = 1
    }

    private static readonly MemberRole[] ElevatedRoles =
    [
        MemberRole.Maintainer,
        MemberRole.Admin,
        MemberRole.SportsDirector,
        MemberRole.YouthSportsDirector,
        MemberRole.Trainer,
    ];

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : AsyncRequestHandler<EditBooking>
    {
        protected override async Task Handle(EditBooking request, CancellationToken cancellationToken)
        {
            var existingBooking = await context.Bookings
                .Include(b => b.Players)
                .SingleRequiredAsync(b => b.Id == request.BookingEntryId, cancellationToken);

            var gracePeriod = await context.Clubs.Select(x => x.BookingGracePeriodInMinutes).SingleRequiredAsync(cancellationToken);

            var memberRoles = await context.Set<Member>()
                .Where(m => m.UserId == request.UserId && m.ClubId == existingBooking.ClubId)
                .OrderBy(m => m.MemberType) // ClubMember (0) before GuestMember (1)
                .Select(m => m.UserRoles)
                .FirstOrDefaultAsync(cancellationToken);

            var hasElevatedRole = memberRoles?.Any(r => ElevatedRoles.Contains(r)) ?? false;

            if (!hasElevatedRole && existingBooking.Interval.From < DateTimeOffset.UtcNow.AddMinutes((-gracePeriod) ?? 0))
                throw new PreconditionException(ErrorCode.CannotEditBookingFromThePast, "Cannot edit booking in the past");

            var queryResult = await (
                from club in context.Clubs
                join court in context.Courts on club.Id equals court.ClubId
                join playMode in context.PlayModes on club.Id equals playMode.ClubId
                where court.Id == request.CourtId && playMode.Id == request.PlayModeId
                select new
                {
                    Club = club,
                    Court = court,
                    PlayMode = playMode,
                }
            )
                .AsNoTracking()
                .SingleRequiredAsync(cancellationToken);

            var localParticipants = await (
                from member in context.Set<Member>()
                join user in context.Users on member.UserId equals user.Id

                let isChild = user.Birthday.AddYears(queryResult.Club.PrimeTimeSettings.ChildAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

                let upcomingBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                        && bookingPlayer.Booking.Id != request.BookingEntryId
                    select bookingPlayer.Id
                ).Count()

                let activeBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.Intersects(request.Interval)
                        && bookingPlayer.Booking.Id != request.BookingEntryId
                    select bookingPlayer.Id
                ).Count()

                let bookingsOnThatDay = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To.Date == request.Interval.From.Date
                        && bookingPlayer.Booking.Id != request.BookingEntryId
                    select bookingPlayer.Id
                ).Count()

                let playModeBookingsInSeasonCount = (
                    from bookingPlayer in context.BookingPlayers
                    where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
                        && bookingPlayer.Booking.Id != request.BookingEntryId
                    join season in context.Seasons on queryResult.Club.Id equals season.ClubId
                    let today = DateOnly.FromDateTime(DateTime.UtcNow)
                    where season.Period.From <= today && today <= season.Period.To
                    where DateOnly.FromDateTime(bookingPlayer.Booking.Interval.From.DateTime) >= season.Period.From
                        && DateOnly.FromDateTime(bookingPlayer.Booking.Interval.To.DateTime) <= season.Period.To
                    select bookingPlayer.Id
                ).Count()

                let isAllowedUser = user.Id == request.UserId
                    || (from familyMember in context.FamilyMembers join member in context.ClubMembers on familyMember.MemberId equals member.Id select member.UserId).Any(userId =>
                        userId == request.UserId
                    )

                where request.Players.Contains(member.Id)
                select new ParticipantInfo
                {
                    IsAllowedUser = isAllowedUser,
                    Member = member,
                    FullName = user.FullName,
                    IsChild = isChild,
                    UpcomingBookingsCount = upcomingBookingsCount,
                    ActiveBookingsCount = activeBookingsCount,
                    BookingsOnThatDay = bookingsOnThatDay,
                    PlayModeBookingsInSeasonCount = playModeBookingsInSeasonCount,
                    IsFromAtpClub = false,
                }
            )
                .AsSingleQuery()
                .AsNoTracking()
                .ExpandProjectables()
                .ToListAsync(cancellationToken);

            var participants = new List<ParticipantInfo>(localParticipants);

            var missingPlayerIds = request.Players.Except(localParticipants.Select(p => p.Member.Id)).ToList();
            if (missingPlayerIds.Count > 0 && queryResult.Club.IsAtpClub)
            {
                var atpParticipants = await (
                    from member in context.ClubMembers.IgnoreQueryFilters()
                    join user in context.Users on member.UserId equals user.Id
                    join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id

                    let isChild = user.Birthday.AddYears(queryResult.Club.PrimeTimeSettings.ChildAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

                    let upcomingBookingsCount = (
                        from bookingPlayer in context.BookingPlayers
                        join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                        where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                            && bookingPlayer.Booking.Id != request.BookingEntryId
                        select bookingPlayer.Id
                    ).Count()

                    let activeBookingsCount = (
                        from bookingPlayer in context.BookingPlayers
                        join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                        where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.Intersects(request.Interval)
                            && bookingPlayer.Booking.Id != request.BookingEntryId
                        select bookingPlayer.Id
                    ).Count()

                    let bookingsOnThatDay = (
                        from bookingPlayer in context.BookingPlayers
                        join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                        where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To.Date == request.Interval.From.Date
                            && bookingPlayer.Booking.Id != request.BookingEntryId
                        select bookingPlayer.Id
                    ).Count()

                    let playModeBookingsInSeasonCount = (
                        from bookingPlayer in context.BookingPlayers
                        where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
                            && bookingPlayer.Booking.Id != request.BookingEntryId
                        join season in context.Seasons on queryResult.Club.Id equals season.ClubId
                        let today = DateOnly.FromDateTime(DateTime.UtcNow)
                        where season.Period.From <= today && today <= season.Period.To
                        where DateOnly.FromDateTime(bookingPlayer.Booking.Interval.From.DateTime) >= season.Period.From
                            && DateOnly.FromDateTime(bookingPlayer.Booking.Interval.To.DateTime) <= season.Period.To
                        select bookingPlayer.Id
                    ).Count()

                    where missingPlayerIds.Contains(member.Id)
                        && club.IsAtpClub
                        && club.Id != queryResult.Club.Id
                    select new ParticipantInfo
                    {
                        IsAllowedUser = true,
                        Member = member,
                        FullName = user.FullName,
                        IsChild = isChild,
                        UpcomingBookingsCount = upcomingBookingsCount,
                        ActiveBookingsCount = activeBookingsCount,
                        BookingsOnThatDay = bookingsOnThatDay,
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

            if (participants.Count != request.Players.Count)
                throw new EntityNotFoundException("Player does not exist");

            if (!participants.Any(i => i.IsAllowedUser))
                throw new PreconditionException(ErrorCode.UserNotAllowedToBook, "User is not allowed to make booking for the provided memberIds");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activeSeason = await context.Seasons
                .Where(s => s.ClubId == queryResult.Club.Id && s.Period.From <= today && today <= s.Period.To)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var memberSeasonMemberIds = activeSeason is not null
                ? await context.MemberSeasons
                    .Where(ms => ms.SeasonId == activeSeason.Id && request.Players.Contains(ms.MemberId))
                    .Select(ms => ms.MemberId)
                    .ToListAsync(cancellationToken)
                : [];

            var intersectingBookings = await context
                .Bookings.Where(b => b.CourtId == request.CourtId && b.Interval.Intersects(request.Interval) && b.Id != request.BookingEntryId)
                .AsNoTracking()
                .ExpandProjectables()
                .ToListAsync(cancellationToken);

            var bookingContext = new BookingDomainService.Context
            {
                ClubId = queryResult.Club.Id,
                Court = queryResult.Court,
                OpeningHours = queryResult.Club.OpeningHours,
                PrimeTimeSettings = queryResult.Club.PrimeTimeSettings,
                BookingMemberRole = [MemberRole.User],
                Participants = participants.ConvertAll(i => new BookingDomainService.Context.PlayerContext
                {
                    Member = i.Member,
                    FullName = i.FullName,
                    IsChild = i.IsChild,
                    UpcomingBookingsCount = i.UpcomingBookingsCount,
                    ActiveBookingsAtTimeOfReservation = i.ActiveBookingsCount,
                    BookingsOnThatDay = i.BookingsOnThatDay,
                    IsAllowedInCurrentSeason = memberSeasonMemberIds.Contains(i.Member.Id),
                    IsAtpParticipant = queryResult.Club.IsAtpClub && i.IsFromAtpClub && i.Member.MemberType == MemberType.ClubMember,
                    PlayModeBookingsInSeasonCount = i.PlayModeBookingsInSeasonCount,
                }),
                IntersectingBookings = intersectingBookings,
                PlayMode = queryResult.PlayMode,
                ClientTimeZoneOffset = request.ClientTimeZoneOffset,
                BookingGracePeriodInMinutes = queryResult.Club.BookingGracePeriodInMinutes,
                ActiveSeasonId = activeSeason?.Id,
                SkipPastBookingValidation = hasElevatedRole,
                CourtBlockingTitles = await context.GetCourtBlockingTitles(request.CourtId, request.Interval, cancellationToken),
            };

            bookingService.EditBooking(bookingContext, existingBooking, request.Interval, request.TimeZoneInfoId, request.Comment);

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record ParticipantInfo
    {
        public required bool IsAllowedUser { get; init; }
        public required Member Member { get; init; }
        public required string FullName { get; init; }
        public required bool IsChild { get; init; }
        public required int UpcomingBookingsCount { get; init; }
        public required int ActiveBookingsCount { get; init; }
        public required int BookingsOnThatDay { get; init; }
        public required int PlayModeBookingsInSeasonCount { get; init; }
        public required bool IsFromAtpClub { get; init; }
    }
}
