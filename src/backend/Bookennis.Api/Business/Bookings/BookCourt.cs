using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record BookCourt(
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    int? ClientTimeZoneOffset,
    int UserId
) : ICommand<BookCourtResult>
{
    public enum ErrorCode
    {
        UserNotAllowedToBook = 0
    }

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : IRequestHandler<BookCourt, BookCourtResult>
    {
        public async Task<BookCourtResult> Handle(BookCourt request, CancellationToken cancellationToken)
        {
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
                join familyMember in context.FamilyMembers on member.Id equals familyMember.MemberId into tmpFamilyMember

                let isChild = user.Birthday.AddYears(queryResult.Club.PrimeTimeSettings.ChildAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

                let upcomingBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                    select bookingPlayer.Id
                ).Count()

                let activeBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.Intersects(request.Interval)
                    select bookingPlayer.Id
                ).Count()

                let bookingsOnThatDay = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To.Date == request.Interval.From.Date
                    select bookingPlayer.Id
                ).Count()

                let playModeBookingsInSeasonCount = (
                    from bookingPlayer in context.BookingPlayers
                    where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
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

            // If the current club is an ATP club, fetch cross-club ATP members that weren't found locally
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
                        select bookingPlayer.Id
                    ).Count()

                    let activeBookingsCount = (
                        from bookingPlayer in context.BookingPlayers
                        join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                        where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.Intersects(request.Interval)
                        select bookingPlayer.Id
                    ).Count()

                    let bookingsOnThatDay = (
                        from bookingPlayer in context.BookingPlayers
                        join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                        where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To.Date == request.Interval.From.Date
                        select bookingPlayer.Id
                    ).Count()

                    let playModeBookingsInSeasonCount = (
                        from bookingPlayer in context.BookingPlayers
                        where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
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
                .Bookings.Where(b => b.CourtId == request.CourtId && b.Interval.Intersects(request.Interval))
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
                CourtBlockingTitles = await context.GetCourtBlockingTitles(request.CourtId, request.Interval, cancellationToken),
            };

            var booking = context.Add(bookingService.BookCourt(bookingContext, request.Interval, request.TimeZoneInfoId, request.Comment)).Entity;
            await context.SaveChangesAsync(cancellationToken);

            return new BookCourtResult { Id = booking.Id };
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
