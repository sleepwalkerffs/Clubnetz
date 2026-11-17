using System.Globalization;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.CourtSales;
using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

/// <remarks><c>TimeZoneInfoId</c>: Ignored: the series is planned in the club's time zone (<c>Club.TimeZoneId</c>).</remarks>
public record BookRecurringCourt(
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    string? TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    [Required] int RecurrenceIntervalWeeks,
    DateOnly? EndDate,
    string? Comment,
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

            // The series is planned in the club's time zone (never the browser's): "today", the weekday and the times of the occurrences
            var region = ClubRegion.Of(queryResult.Club);
            var tz = region.TimeZone;

            // Determine series end date
            var today = region.Today();
            var activeSeason = await context.Seasons
                .Where(s => s.ClubId == queryResult.Club.Id && s.Period.From <= today && today <= s.Period.To)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken) ?? throw new PreconditionException(BookingDomainService.ErrorCode.NoActiveSeasonForClub, "No active season for the club");

            var seriesEndDate = request.EndDate ?? activeSeason.Period.To;
            if (seriesEndDate > activeSeason.Period.To)
                seriesEndDate = activeSeason.Period.To;

            var enrollments = await context.MemberSeasons
                .Where(ms => ms.SeasonId == activeSeason.Id && request.Players.Contains(ms.MemberId))
                .ToDictionaryAsync(ms => ms.MemberId, ms => ms.Sports, cancellationToken);

            // Calculate all occurrence dates
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
            var mayBookForAnyMember = await BookingRights.MayBookForAnyMember(context, queryResult.Club.Id, request.UserId, request.PlayModeId, cancellationToken);
            var localParticipants = await LoadParticipants(request, queryResult.Club, region, activeSeason, firstInterval, mayBookForAnyMember, cancellationToken);

            if (localParticipants.Count != request.Players.Count)
                throw new EntityNotFoundException("Player does not exist");

            if (!localParticipants.Any(i => i.IsAllowedUser))
                throw new PreconditionException(ErrorCode.UserNotAllowedToBook, "User is not allowed to make booking for the provided memberIds");

            // Bookings of the club one after the other, so the overlap checks see every booking saved before
            await context.LockCourts(queryResult.Club.Id, cancellationToken);

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
                TimeZone = tz,
                FormatTime = region.Culture(CultureInfo.CurrentCulture).Time,
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
                    IsAllowedInCurrentSeason = enrollments.ContainsKey(i.Member.Id),
                    EnrolledSports = enrollments.GetValueOrDefault(i.Member.Id, []),
                    IsAtpParticipant = false,
                    PlayModeBookingsInSeasonCount = i.PlayModeBookingsInSeasonCount,
                }),
                IntersectingBookings = intersectingBookings,
                PlayMode = queryResult.PlayMode,
                BookingGracePeriodInMinutes = queryResult.Club.BookingGracePeriodInMinutes,
                ClubBookingsPerWeek = queryResult.Club.ConcurrentAllowedBookings,
                ActiveSeasonId = activeSeason.Id,
                CourtBlockingTitles = await context.GetCourtBlockingTitles(request.CourtId, firstInterval, cancellationToken),
            };

            // Validate first occurrence via domain service
            bookingService.BookCourt(bookingContext, firstInterval, region.TimeZoneId, request.Comment);

            // Create the recurring series
            var series = new RecurringBookingSeries(
                queryResult.Club.Id,
                request.CourtId,
                request.PlayModeId,
                dayOfWeek,
                startTime,
                endTime,
                region.TimeZoneId,
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
                var occurrenceStart = ClubTimeZone.At(date, startTime, tz);
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
                    region.TimeZoneId,
                    request.Players,
                    request.Comment,
                    series.Id);

                context.Bookings.Add(booking);
                createdBookings.Add(booking);
            }

            await context.SaveChangesAsync(cancellationToken);

            return new BookCourtResult { Id = createdBookings.First().Id };
        }

        private async Task<List<ParticipantInfo>> LoadParticipants(BookRecurringCourt request, Domain.Clubs.Club club, ClubRegion region, Domain.Clubs.Season activeSeason, DateTimeOffsetInterval interval, bool mayBookForAnyMember, CancellationToken cancellationToken)
        {
            // Local day of the club: the day of the booking, the age of the children and the season
            var today = region.Today();
            var childAgeThreshold = club.PrimeTimeSettings.ChildAgeThreshold;
            var day = region.LocalDate(interval.From);
            var dayStart = region.StartOfDay(day).ToUniversalTime();
            var dayEnd = region.StartOfDay(day.AddDays(1)).ToUniversalTime();
            var seasonStart = region.StartOfDay(activeSeason.Period.From).ToUniversalTime();
            var seasonEnd = region.StartOfDay(activeSeason.Period.To.AddDays(1)).ToUniversalTime();

            return await (
                from member in context.Set<Member>()
                join user in context.Users on member.UserId equals user.Id

                let isChild = user.Birthday.AddYears(childAgeThreshold) > today

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
                    where playMode.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To >= dayStart && bookingPlayer.Booking.Interval.To < dayEnd
                    select bookingPlayer.Id
                ).Count()

                let playModeBookingsInSeasonCount = (
                    from bookingPlayer in context.BookingPlayers
                    where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
                    where bookingPlayer.Booking.Interval.From >= seasonStart && bookingPlayer.Booking.Interval.To <= seasonEnd
                    select bookingPlayer.Id
                ).Count()

                // Same rule as BookingContextBuilder: staff, the user themself, or a child of a family the user is a parent of
                let isAllowedUser = mayBookForAnyMember
                    || user.Id == request.UserId
                    || context.Families.Any(family =>
                        family.Children.Any(child => child.MemberId == member.Id)
                        && family.Parents.Any(parent => context.ClubMembers.Any(parentMember => parentMember.Id == parent.MemberId && parentMember.UserId == request.UserId)))

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
