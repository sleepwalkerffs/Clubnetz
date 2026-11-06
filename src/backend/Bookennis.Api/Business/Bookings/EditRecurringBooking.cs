using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Business.CourtSales;
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

        /// <summary>
        /// Moves or changes one occurrence: exactly the rules of <see cref="EditBooking"/> (shared <see cref="BookingEditRules"/> and
        /// <see cref="BookingContextBuilder"/>), then the occurrence is excluded from later series edits.
        /// </summary>
        private async Task HandleSingleEdit(EditRecurringBooking request, Booking booking, CancellationToken cancellationToken)
        {
            var hasElevatedRole = await BookingEditRules.EnsureMayEdit(context, booking, request.UserId, cancellationToken);

            var builder = await BookingContextBuilder.Load(
                context,
                request.PlayModeId,
                [request.CourtId],
                request.Players,
                request.UserId,
                [request.Interval],
                cancellationToken,
                editedBookingId: booking.Id);

            var bookingContext = await builder.BuildContext(request.CourtId, request.Interval, [], cancellationToken)
                with { SkipPastBookingValidation = hasElevatedRole };

            bookingService.EditBooking(bookingContext, booking, request.Interval, request.TimeZoneInfoId, request.Comment);
            booking.ExcludeFromSeries();

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task HandleSeriesEdit(EditRecurringBooking request, Booking booking, CancellationToken cancellationToken)
        {
            if (booking.RecurringBookingSeriesId is null)
                throw new PreconditionException(ErrorCode.BookingNotPartOfSeries, "Booking is not part of a recurring series");

            var hasElevatedRole = await BookingEditRules.EnsureMayEdit(context, booking, request.UserId, cancellationToken);

            var series = await context.RecurringBookingSeries
                .Include(s => s.SeriesPlayers)
                .SingleAsync(s => s.Id == booking.RecurringBookingSeriesId, cancellationToken);

            var tz = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneInfoId);
            // The date the member picked: moving the occurrence to another day moves the series to that weekday
            var editFromDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(request.Interval.From.UtcDateTime, tz));

            await context.LockCourts(series.ClubId, cancellationToken);

            // The later occurrences go (individually edited ones stay) and are created again below. They are removed before the
            // edited occurrence is checked, so moving it onto the slot of a later occurrence is not a conflict with the own series.
            // A failed check rolls this back with the transaction of the command.
            var laterBookings = await context.Bookings
                .Where(b => b.RecurringBookingSeriesId == series.Id
                    && b.Id != booking.Id
                    && b.Interval.From >= booking.Interval.From
                    && !b.IsExcludedFromSeries)
                .ToListAsync(cancellationToken);

            context.Bookings.RemoveRange(laterBookings);
            await context.SaveChangesAsync(cancellationToken);

            // The edited occurrence keeps its id (the detail page stays on it) and goes through exactly the rules of a single edit:
            // who may book for the players, opening hours, inactive or blocked courts, conflicts, enrollment
            var builder = await BookingContextBuilder.Load(
                context,
                request.PlayModeId,
                [request.CourtId],
                request.Players,
                request.UserId,
                [request.Interval],
                cancellationToken,
                editedBookingId: booking.Id);

            var bookingContext = await builder.BuildContext(request.CourtId, request.Interval, [], cancellationToken)
                with { SkipPastBookingValidation = hasElevatedRole };

            bookingService.EditBooking(bookingContext, booking, request.Interval, request.TimeZoneInfoId, request.Comment);

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

            // Regenerate the later occurrences, the edited one is the first
            var current = editFromDate.AddDays(7 * newIntervalWeeks);
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
