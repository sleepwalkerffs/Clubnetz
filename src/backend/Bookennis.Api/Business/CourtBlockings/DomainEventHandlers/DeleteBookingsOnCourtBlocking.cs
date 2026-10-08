using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Business.Shared.DomainEventHandlers;
using Bookennis.Api.Data;
using Bookennis.Domain.Courts.Events;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtBlockings.DomainEventHandlers;

/// <summary>Deletes the upcoming bookings in the blocked time and notifies their players with the title of the blocking as reason.</summary>
public class DeleteBookingsOnCourtBlocking(AppDbContext context, IMediator mediator, IUserAccessor userAccessor, IBookingPushNotifier pushNotifier)
    : DeleteBookingsNotificationHandler<CourtBlockedDomainEvent>(context, mediator, userAccessor, pushNotifier),
        INotificationHandler<DomainEvent<CourtBlockedDomainEvent>>
{
    protected override async Task<List<DeletedBookings>> QueryBookingsToBeDeleted(DomainEvent<CourtBlockedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var blocking = await Context.CourtBlockings
            .Include(b => b.Courts)
            .Include(b => b.Occurrences)
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.Id == notification.EntityId, cancellationToken);

        if (blocking is null)
            return [];

        var conflicts = await Context.GetConflictingBookings(blocking, DateTimeOffset.UtcNow, cancellationToken);
        var bookingIds = conflicts.ConvertAll(b => b.Id);
        if (bookingIds.Count == 0)
            return [];

        var title = blocking.Title;

        return await (
            from booking in Context.Bookings
            join court in Context.Courts on booking.CourtId equals court.Id
            where bookingIds.Contains(booking.Id)
            select new DeletedBookings
            {
                Booking = booking,
                CourtName = court.Name,
                ReasonKey = "ReasonCourtBlocked",
                ReasonFormatArg = title,
            }
        )
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }
}
