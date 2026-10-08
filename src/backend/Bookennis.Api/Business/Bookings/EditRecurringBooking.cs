using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record EditRecurringBooking(
    [Required] int BookingEntryId,
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    [Required] RecurringEditScope Scope,
    int? RecurrenceIntervalWeeks,
    DateOnly? EndDate,
    string? Comment,
    int? ClientTimeZoneOffset,
    int UserId
) : ICommand
{
    public enum ErrorCode
    {
        BookingNotPartOfSeries = 0
    }

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : AsyncRequestHandler<EditRecurringBooking>
    {
        protected override async Task Handle(EditRecurringBooking request, CancellationToken cancellationToken)
        {
            var booking = await context.Bookings
                .Include(b => b.Players)
                .SingleAsync(b => b.Id == request.BookingEntryId, cancellationToken);

            if (request.Scope == RecurringEditScope.Single)
            {
                await HandleSingleEdit(request, booking, cancellationToken);
            }
            else
            {
                await HandleSeriesEdit(request, booking, cancellationToken);
            }
        }

        private async Task HandleSingleEdit(EditRecurringBooking request, Booking booking, CancellationToken cancellationToken)
        {
            var queryResult = await (
                from club in context.Clubs
                join court in context.Courts on club.Id equals court.ClubId
                join playMode in context.PlayModes on club.Id equals playMode.ClubId
                where court.Id == request.CourtId && playMode.Id == request.PlayModeId
                select new { Club = club, Court = court, PlayMode = playMode }
            ).AsNoTracking().SingleRequiredAsync(cancellationToken);

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

            var participants = await (
                from member in context.Set<Member>()
                join user in context.Users on member.UserId equals user.Id

                let isChild = user.Birthday.AddYears(queryResult.Club.PrimeTimeSettings.ChildAgeThreshold) > DateOnly.FromDateTime(DateTime.UtcNow)

                let upcomingBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join pm in context.PlayModes on bookingPlayer.Booking.PlayModeId equals pm.Id
                    where pm.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To > DateTimeOffset.UtcNow
                    select bookingPlayer.Id
                ).Count()

                let activeBookingsCount = (
                    from bookingPlayer in context.BookingPlayers
                    join pm in context.PlayModes on bookingPlayer.Booking.PlayModeId equals pm.Id
                    where pm.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.Intersects(request.Interval)
                        && bookingPlayer.BookingEntryId != booking.Id
                    select bookingPlayer.Id
                ).Count()

                let bookingsOnThatDay = (
                    from bookingPlayer in context.BookingPlayers
                    join pm in context.PlayModes on bookingPlayer.Booking.PlayModeId equals pm.Id
                    where pm.IsChargingBookingSubscription && bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.Interval.To.Date == request.Interval.From.Date
                        && bookingPlayer.BookingEntryId != booking.Id
                    select bookingPlayer.Id
                ).Count()

                let playModeBookingsInSeasonCount = (
                    from bookingPlayer in context.BookingPlayers
                    where bookingPlayer.MemberId == member.Id && bookingPlayer.Booking.PlayModeId == request.PlayModeId
                    join season in context.Seasons on queryResult.Club.Id equals season.ClubId
                    let td = DateOnly.FromDateTime(DateTime.UtcNow)
                    where season.Period.From <= td && td <= season.Period.To
                    where DateOnly.FromDateTime(bookingPlayer.Booking.Interval.From.DateTime) >= season.Period.From
                        && DateOnly.FromDateTime(bookingPlayer.Booking.Interval.To.DateTime) <= season.Period.To
                    select bookingPlayer.Id
                ).Count()

                where request.Players.Contains(member.Id)
                select new
                {
                    Member = member,
                    user.FullName,
                    IsChild = isChild,
                    UpcomingBookingsCount = upcomingBookingsCount,
                    ActiveBookingsCount = activeBookingsCount,
                    BookingsOnThatDay = bookingsOnThatDay,
                    PlayModeBookingsInSeasonCount = playModeBookingsInSeasonCount,
                }
            ).AsSingleQuery().AsNoTracking().ExpandProjectables().ToListAsync(cancellationToken);

            var intersectingBookings = await context
                .Bookings.Where(b => b.CourtId == request.CourtId && b.Interval.Intersects(request.Interval) && b.Id != booking.Id)
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
                    IsAtpParticipant = false,
                    PlayModeBookingsInSeasonCount = i.PlayModeBookingsInSeasonCount,
                }),
                IntersectingBookings = intersectingBookings,
                PlayMode = queryResult.PlayMode,
                ClientTimeZoneOffset = request.ClientTimeZoneOffset,
                BookingGracePeriodInMinutes = queryResult.Club.BookingGracePeriodInMinutes,
                ActiveSeasonId = activeSeason?.Id,
                CourtBlockingTitles = await context.GetCourtBlockingTitles(request.CourtId, request.Interval, cancellationToken),
            };

            bookingService.EditBooking(bookingContext, booking, request.Interval, request.TimeZoneInfoId, request.Comment);
            booking.ExcludeFromSeries();

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task HandleSeriesEdit(EditRecurringBooking request, Booking booking, CancellationToken cancellationToken)
        {
            if (booking.RecurringBookingSeriesId is null)
                throw new PreconditionException(ErrorCode.BookingNotPartOfSeries, "Booking is not part of a recurring series");

            var series = await context.RecurringBookingSeries
                .Include(s => s.SeriesPlayers)
                .SingleAsync(s => s.Id == booking.RecurringBookingSeriesId, cancellationToken);

            var tz = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneInfoId);
            var editFromDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(booking.Interval.From.UtcDateTime, tz));

            // Delete all future bookings in the series from this date onward (excluding individually edited ones)
            var futureBookings = await context.Bookings
                .Where(b => b.RecurringBookingSeriesId == series.Id
                    && b.Interval.From >= booking.Interval.From
                    && !b.IsExcludedFromSeries)
                .ToListAsync(cancellationToken);

            context.Bookings.RemoveRange(futureBookings);

            // Update series template
            var newStartTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.Interval.From.UtcDateTime, tz));
            var newEndTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.Interval.To.UtcDateTime, tz));
            var newIntervalWeeks = request.RecurrenceIntervalWeeks ?? series.RecurrenceIntervalWeeks;

            series.Update(
                request.CourtId,
                request.PlayModeId,
                editFromDate.DayOfWeek,
                newStartTime,
                newEndTime,
                request.TimeZoneInfoId,
                newIntervalWeeks,
                editFromDate,
                request.EndDate ?? series.EndDate,
                request.Comment,
                request.Players);

            await context.SaveChangesAsync(cancellationToken);

            // Determine end date for regeneration
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activeSeason = await context.Seasons
                .Where(s => s.ClubId == series.ClubId && s.Period.From <= today && today <= s.Period.To)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var seriesEndDate = series.EndDate ?? activeSeason?.Period.To ?? editFromDate.AddMonths(6);
            var duration = request.Interval.To - request.Interval.From;

            // Regenerate future bookings
            var current = editFromDate;
            var playMode = await context.PlayModes.FindRequiredAsync(request.PlayModeId, cancellationToken);

            while (current <= seriesEndDate)
            {
                var occurrenceStart = new DateTimeOffset(current.ToDateTime(newStartTime), tz.GetUtcOffset(current.ToDateTime(newStartTime)));
                var occurrenceEnd = occurrenceStart.Add(duration);
                var occurrenceInterval = new DateTimeOffsetInterval(occurrenceStart.ToUniversalTime(), occurrenceEnd.ToUniversalTime());

                // Skip dates where an individually edited (excluded) booking from this series exists
                var dayStart = new DateTimeOffset(current.ToDateTime(TimeOnly.MinValue), tz.GetUtcOffset(current.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
                var dayEnd = dayStart.AddDays(1);
                var hasExcludedBooking = await context.Bookings
                    .AnyAsync(b => b.IsExcludedFromSeries
                        && b.RecurringBookingSeriesId == series.Id
                        && b.Interval.From >= dayStart
                        && b.Interval.From < dayEnd, cancellationToken);

                if (hasExcludedBooking)
                {
                    current = current.AddDays(7 * newIntervalWeeks);
                    continue;
                }

                if (!playMode.CanOverbook)
                {
                    var hasConflict = await context.Bookings
                        .AnyAsync(b => b.CourtId == request.CourtId && b.Interval.Intersects(occurrenceInterval), cancellationToken);
                    if (hasConflict)
                    {
                        current = current.AddDays(7 * newIntervalWeeks);
                        continue;
                    }
                }

                // Blocked courts can't be booked, not even with an overbooking play mode
                if (await context.IsCourtBlocked(request.CourtId, occurrenceInterval, cancellationToken))
                {
                    current = current.AddDays(7 * newIntervalWeeks);
                    continue;
                }

                var newBooking = new Booking(
                    series.ClubId,
                    request.CourtId,
                    request.PlayModeId,
                    occurrenceInterval,
                    request.TimeZoneInfoId,
                    request.Players,
                    request.Comment,
                    series.Id);

                context.Bookings.Add(newBooking);
                current = current.AddDays(7 * newIntervalWeeks);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
