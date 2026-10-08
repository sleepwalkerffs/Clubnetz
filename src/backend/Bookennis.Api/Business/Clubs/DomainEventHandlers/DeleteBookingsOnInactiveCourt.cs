using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Business.Shared.DomainEventHandlers;
using Bookennis.Api.Data;
using Bookennis.Domain.Courts.Events;
using Bookennis.Global.Intervals;
using EntityFrameworkCore.Projectables.Extensions;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs.DomainEventHandlers;

public class DeleteBookingsOnInactiveCourt(AppDbContext context, IMediator mediator, IUserAccessor userAccessor, IBookingPushNotifier pushNotifier)
    : DeleteBookingsNotificationHandler<CourtSetInactiveDomainEvent>(context, mediator, userAccessor, pushNotifier),
        INotificationHandler<DomainEvent<CourtSetInactiveDomainEvent>>
{
    protected override async Task<List<DeletedBookings>> QueryBookingsToBeDeleted(DomainEvent<CourtSetInactiveDomainEvent> notification, CancellationToken cancellationToken)
    {
        var court = await Context.Courts.FindRequiredAsync(notification.EntityId, cancellationToken);
        if (court.Inactive is null)
            return [];

        var intersectingBookings = await (
            from booking in Context.Bookings
            where booking.CourtId == court.Id && booking.Interval.Intersects(court.Inactive)
            select new DeletedBookings
            {
                Booking = booking,
                CourtName = court.Name,
                ReasonKey = "ReasonCourtInactive",
            }
        )
            .AsSplitQuery()
            .ExpandProjectables()
            .ToListAsync(cancellationToken);
        return intersectingBookings;
    }
}
