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

public class DeleteIntersectingBookings(AppDbContext context, IMediator mediator, IUserAccessor userAccessor, IBookingPushNotifier pushNotifier)
    : DeleteBookingsNotificationHandler<BookingAddedDomainEvent>(context, mediator, userAccessor, pushNotifier),
        INotificationHandler<DomainEvent<BookingAddedDomainEvent>>
{
    protected override async Task<List<DeletedBookings>> QueryBookingsToBeDeleted(DomainEvent<BookingAddedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var newBooking = await (
            from booking in Context.Bookings
            join playMode in Context.PlayModes on booking.PlayModeId equals playMode.Id
            where booking.Id == notification.EntityId
            select new { Booking = booking, PlayModeName = playMode.Name }
        ).SingleRequiredAsync(cancellationToken);

        var intersectingBookings = await (
            from booking in Context.Bookings
            join court in Context.Courts on booking.CourtId equals court.Id
            where booking.Id != newBooking.Booking.Id && booking.CourtId == newBooking.Booking.CourtId && booking.Interval.Intersects(newBooking.Booking.Interval)
            select new DeletedBookings
            {
                Booking = booking,
                CourtName = court.Name,
                ReasonKey = "ResonOverbooked",
                ReasonFormatArg = newBooking.PlayModeName,
            }
        )
            .AsSplitQuery()
            .ExpandProjectables()
            .ToListAsync(cancellationToken);

        return intersectingBookings;
    }
}
