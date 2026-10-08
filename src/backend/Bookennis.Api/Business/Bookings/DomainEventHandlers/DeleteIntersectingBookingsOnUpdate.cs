using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Business.Shared.DomainEventHandlers;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings.Events;
using Bookennis.Global.Intervals;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings.DomainEventHandlers;

public class DeleteIntersectingBookingsOnUpdate(AppDbContext context, IMediator mediator, IUserAccessor userAccessor, IBookingPushNotifier pushNotifier)
    : DeleteBookingsNotificationHandler<BookingUpdatedDomainEvent>(context, mediator, userAccessor, pushNotifier),
        INotificationHandler<DomainEvent<BookingUpdatedDomainEvent>>
{
    protected override async Task<List<DeletedBookings>> QueryBookingsToBeDeleted(DomainEvent<BookingUpdatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var updatedBooking = await (
            from booking in Context.Bookings
            join playMode in Context.PlayModes on booking.PlayModeId equals playMode.Id
            where booking.Id == notification.EntityId
            select new { Booking = booking, PlayModeName = playMode.Name }
        ).SingleRequiredAsync(cancellationToken);

        var intersectingBookings = await (
            from booking in Context.Bookings
            join court in Context.Courts on booking.CourtId equals court.Id
            where booking.Id != updatedBooking.Booking.Id && booking.CourtId == updatedBooking.Booking.CourtId && booking.Interval.Intersects(updatedBooking.Booking.Interval)
            select new DeletedBookings
            {
                Booking = booking,
                CourtName = court.Name,
                ReasonKey = "ResonOverbooked",
                ReasonFormatArg = updatedBooking.PlayModeName,
            }
        )
            .AsSplitQuery()
            .ExpandProjectables()
            .ToListAsync(cancellationToken);

        return intersectingBookings;
    }
}
