using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Booking;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record DeleteRecurringBooking(
    [Required] int BookingEntryId,
    [Required] RecurringDeleteScope Scope
) : ICommand
{
    public enum ErrorCode
    {
        BookingNotPartOfSeries = 0
    }

    public class Handler(AppDbContext context, IBookingPushNotifier pushNotifier, IBookingEmailNotifier emailNotifier) : AsyncRequestHandler<DeleteRecurringBooking>
    {
        protected override async Task Handle(DeleteRecurringBooking request, CancellationToken cancellationToken)
        {
            var gracePeriod = await context.Clubs.Select(x => x.BookingGracePeriodInMinutes).SingleRequiredAsync(cancellationToken);
            var booking = await context.Bookings.FindRequiredAsync(request.BookingEntryId, cancellationToken);

            if (booking.Interval.From < DateTimeOffset.UtcNow.AddMinutes((-gracePeriod) ?? 0))
                throw new PreconditionException(DeleteBooking.ErrorCode.CannotDeleteBookingFromThePast, "Cannot delete booking in the past");

            if (request.Scope == RecurringDeleteScope.Single)
            {
                await pushNotifier.BookingDeleted(booking.Id, cancellationToken);
                await emailNotifier.BookingDeleted(booking.Id, cancellationToken);
                context.Bookings.Remove(booking);
            }
            else
            {
                if (booking.RecurringBookingSeriesId is null)
                    throw new PreconditionException(ErrorCode.BookingNotPartOfSeries, "Booking is not part of a recurring series");

                await pushNotifier.RecurringBookingDeleted(booking.Id, cancellationToken);
                await emailNotifier.RecurringBookingDeleted(booking.Id, cancellationToken);

                // Delete all bookings in the series from this date onward
                var futureBookings = await context.Bookings
                    .Where(b => b.RecurringBookingSeriesId == booking.RecurringBookingSeriesId
                        && b.Interval.From >= booking.Interval.From)
                    .ToListAsync(cancellationToken);

                context.Bookings.RemoveRange(futureBookings);

                // Check if there are any remaining bookings in the series
                var hasRemainingBookings = await context.Bookings
                    .AnyAsync(b => b.RecurringBookingSeriesId == booking.RecurringBookingSeriesId
                        && b.Interval.From < booking.Interval.From, cancellationToken);

                if (!hasRemainingBookings)
                {
                    // Delete the series itself if no bookings remain
                    var series = await context.RecurringBookingSeries
                        .FindRequiredAsync(booking.RecurringBookingSeriesId.Value, cancellationToken);
                    context.RecurringBookingSeries.Remove(series);
                }
                else
                {
                    // Update series end date to the day before the deleted booking
                    var series = await context.RecurringBookingSeries
                        .FindRequiredAsync(booking.RecurringBookingSeriesId.Value, cancellationToken);
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(series.TimeZoneInfoId);
                    var bookingDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(booking.Interval.From.UtcDateTime, tz));
                    series.Update(
                        series.CourtId,
                        series.PlayModeId,
                        series.DayOfWeek,
                        series.StartTime,
                        series.EndTime,
                        series.TimeZoneInfoId,
                        series.RecurrenceIntervalWeeks,
                        series.StartDate,
                        bookingDate.AddDays(-1),
                        series.Comment);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
