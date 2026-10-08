using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record DeleteBooking(int BookingEntryId) : ICommand
{
    public enum ErrorCode
    {
        CannotDeleteBookingFromThePast = 0
    }

    public class Handler(AppDbContext context, IBookingPushNotifier pushNotifier, IBookingEmailNotifier emailNotifier) : AsyncRequestHandler<DeleteBooking>
    {
        protected override async Task Handle(DeleteBooking request, CancellationToken cancellationToken)
        {
            var gracePeriod = await context.Clubs.Select(x => x.BookingGracePeriodInMinutes).SingleRequiredAsync(cancellationToken);

            var booking = await context.Bookings.FindRequiredAsync(request.BookingEntryId, cancellationToken);
            if (booking.Interval.From < DateTimeOffset.UtcNow.AddMinutes((-gracePeriod) ?? 0))
                throw new PreconditionException(ErrorCode.CannotDeleteBookingFromThePast, "Cannot delete booking in the past");

            await pushNotifier.BookingDeleted(booking.Id, cancellationToken);
            await emailNotifier.BookingDeleted(booking.Id, cancellationToken);

            context.Bookings.Remove(booking);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
