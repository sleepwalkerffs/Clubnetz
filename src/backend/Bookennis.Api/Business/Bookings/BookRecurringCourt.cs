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

public record BookRecurringCourt(
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    [Required] int RecurrenceIntervalWeeks,
    DateOnly? EndDate,
    string? Comment,
    int? ClientTimeZoneOffset,
    int UserId
) : ICommand<BookCourtResult>
{
    public enum ErrorCode
    {
        RecurrenceIntervalTooSmall = 0,
        PlayModeDoesNotAllowRecurring = 1,
        NoOccurrencesInDateRange = 2,
        UserNotAllowedToBook = 3
    }

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : IRequestHandler<BookRecurringCourt, BookCourtResult>
    {
        public async Task<BookCourtResult> Handle(BookRecurringCourt request, CancellationToken cancellationToken)
        {
            if (request.RecurrenceIntervalWeeks < 1)
                throw new PreconditionException(ErrorCode.RecurrenceIntervalTooSmall, "RecurrenceIntervalWeeks must be at least 1");

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

            if (!queryResult.PlayMode.AllowRecurring)
                throw new PreconditionException(ErrorCode.PlayModeDoesNotAllowRecurring, "Play mode does not allow recurring bookings");

            // Determine series end date
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activeSeason = await context.Seasons
                .Where(s => s.ClubId == queryResult.Club.Id && s.Period.From <= today && today <= s.Period.To)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken) ?? throw new PreconditionException(BookingDomainService.ErrorCode.NoActiveSeasonForClub, "No active season for the club");

            var seriesEndDate = request.EndDate ?? activeSeason.Period.To;
            if (seriesEndDate > activeSeason.Period.To)
                seriesEndDate = activeSeason.Period.To;

            var memberSeasonMemberIds = await context.MemberSeasons
                .Where(ms => ms.SeasonId == activeSeason.Id && request.Players.Contains(ms.MemberId))
                .Select(ms => ms.MemberId)
                .ToListAsync(cancellationToken);

            // Calculate all occurrence dates
            var tz = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneInfoId);
            var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.Interval.From.UtcDateTime, tz));
            var dayOfWeek = firstDate.DayOfWeek;
            var startTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.Interval.From.UtcDateTime, tz));
            var endTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.Interval.To.UtcDateTime, tz));
            var duration = request.Interval.To - request.Interval.From;

            var occurrenceDates = new List<DateOnly>();
            var current = firstDate;
            while (current <= seriesEndDate)
            {
                occurrenceDates.Add(current);
                current = current.AddDays(7 * request.RecurrenceIntervalWeeks);
            }

            if (occurrenceDates.Count == 0)
                throw new PreconditionException(ErrorCode.NoOccurrencesInDateRange, "No occurrences fall within the specified date range");

            // Validate first occurrence fully (load participants etc.)
            var firstInterval = request.Interval;
            var localParticipants = await LoadParticipants(request, queryResult.Club, firstInterval, cancellationToken);

            if (localParticipants.Count != request.Players.Count)
                throw new EntityNotFoundException("Player does not exist");

            if (!localParticipants.Any(i => i.IsAllowedUser))
                throw new PreconditionException(ErrorCode.UserNotAllowedToBook, "User is not allowed to make booking for the provided memberIds");

            var intersectingBookings = await context
                .Bookings.Where(b => b.CourtId == request.CourtId && b.Interval.Intersects(firstInterval))
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
                Participants = localParticipants.ConvertAll(i => new BookingDomainService.Context.PlayerContext
                {
                    Member = i.Member,
                    FullName = i.FullName,
                    IsChild = i.IsChild,
                    UpcomingBookingsCount = i.UpcomingBookingsCount,
                    ActiveBookingsAtTimeOfReservation = i.ActiveBookingsCount,
                    BookingsOnThatDay = i.BookingsOnThatDay,
                    IsAllowedInCurrentSeason = memberSeasonMemberIds.Contains(i.Member.Id),
                    IsAtpParticipant = false,
                    PlayModeBookingsInSeasonCount = i.PlayModeBookingsInSeasonCount,
                }),
                IntersectingBookings = intersectingBookings,
                PlayMode = queryResult.PlayMode,
                ClientTimeZoneOffset = request.ClientTimeZoneOffset,
                BookingGracePeriodInMinutes = queryResult.Club.BookingGracePeriodInMinutes,
                ActiveSeasonId = activeSeason.Id,
                CourtBlockingTitles = await context.GetCourtBlockingTitles(request.CourtId, firstInterval, cancellationToken),
            };

            // Validate first occurrence via domain service
            bookingService.BookCourt(bookingContext, firstInterval, request.TimeZoneInfoId, request.Comment);

            // Create the recurring series
            var series = new RecurringBookingSeries(
                queryResult.Club.Id,
                request.CourtId,
                request.PlayModeId,
                dayOfWeek,
                startTime,
                endTime,
                request.TimeZoneInfoId,
                request.RecurrenceIntervalWeeks,
                firstDate,
                request.EndDate,
                request.Comment,
                request.Players);

            context.RecurringBookingSeries.Add(series);
            await context.SaveChangesAsync(cancellationToken);

            // Create individual booking occurrences
            var createdBookings = new List<Booking>();
            foreach (var date in occurrenceDates)
            {
                var occurrenceStart = new DateTimeOffset(date.ToDateTime(startTime), tz.GetUtcOffset(date.ToDateTime(startTime)));
                var occurrenceEnd = occurrenceStart.Add(duration);
                var occurrenceInterval = new DateTimeOffsetInterval(occurrenceStart.ToUniversalTime(), occurrenceEnd.ToUniversalTime());

                // For non-first occurrences, check court conflicts only if not overbooking
                if (date != firstDate && !queryResult.PlayMode.CanOverbook)
                {
                    var hasConflict = await context.Bookings
                        .AnyAsync(b => b.CourtId == request.CourtId && b.Interval.Intersects(occurrenceInterval), cancellationToken);
                    if (hasConflict)
                        continue; // Skip conflicting occurrences
                }

                // Blocked courts can't be booked, not even with an overbooking play mode
                if (date != firstDate && await context.IsCourtBlocked(request.CourtId, occurrenceInterval, cancellationToken))
                    continue;

                var booking = new Booking(
                    queryResult.Club.Id,
                    request.CourtId,
                    request.PlayModeId,
                    occurrenceInterval,
                    request.TimeZoneInfoId,
                    request.Players,
                    request.Comment,
                    series.Id);

                context.Bookings.Add(booking);
                createdBookings.Add(booking);
            }

            await context.SaveChangesAsync(cancellationToken);

            return new BookCourtResult { Id = createdBookings.First().Id };
        }

        private async Task<List<ParticipantInfo>> LoadParticipants(BookRecurringCourt request, Domain.Clubs.Club club, DateTimeOffsetInterval interval, CancellationToken cancellationToken)
        {
            return await (
                from member in context.Set<Member>()
                join user in context.Users on member.UserId equals user.Id

                let isChild = user.Birthday.AddYears(club.PrimeTimeSettings.ChildAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

                let upcomingBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                    select bookingPlayer.Id
                ).Count()

                let activeBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.Intersects(interval)
                    select bookingPlayer.Id
                ).Count()

                let bookingsOnThatDay = (
                    from bookingPlayer in context.BookingPlayers
                    join playMode in context.PlayModes on bookingPlayer.Booking.PlayModeId equals playMode.Id
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To.Date == interval.From.Date
                    select bookingPlayer.Id
                ).Count()

                let playModeBookingsInSeasonCount = (
                    from bookingPlayer in context.BookingPlayers
                    where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
                    join season in context.Seasons on club.Id equals season.ClubId
                    let today = DateOnly.FromDateTime(DateTime.UtcNow)
                    where season.Period.From <= today && today <= season.Period.To
                    where DateOnly.FromDateTime(bookingPlayer.Booking.Interval.From.DateTime) >= season.Period.From
                        && DateOnly.FromDateTime(bookingPlayer.Booking.Interval.To.DateTime) <= season.Period.To
                    select bookingPlayer.Id
                ).Count()

                let isAllowedUser = user.Id == request.UserId
                    || (from familyMember in context.FamilyMembers join m in context.ClubMembers on familyMember.MemberId equals m.Id select m.UserId).Any(userId =>
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
                }
            )
                .AsSingleQuery()
                .AsNoTracking()
                .ExpandProjectables()
                .ToListAsync(cancellationToken);
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
    }
}
