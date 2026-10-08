using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Notifications;
using Bookennis.Domain.Base;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Bookings.Events;

namespace Bookennis.Api.Business.Push.DomainEventHandlers;

/// <summary>Push notification and email for the other players when somebody books a court with them.</summary>
public class NotifyBookingPlayersHandler(IBookingPushNotifier pushNotifier, IBookingEmailNotifier emailNotifier)
    : INotificationHandler<DomainEvent<BookingAddedDomainEvent>>,
        INotificationHandler<DomainEvent<EntityCreated<RecurringBookingSeries>>>
{
    public async Task Handle(DomainEvent<BookingAddedDomainEvent> notification, CancellationToken cancellationToken)
    {
        await pushNotifier.BookingAdded(notification.EntityId, cancellationToken);
        await emailNotifier.BookingAdded(notification.EntityId, cancellationToken);
    }

    public async Task Handle(DomainEvent<EntityCreated<RecurringBookingSeries>> notification, CancellationToken cancellationToken)
    {
        await pushNotifier.RecurringBookingAdded(notification.EntityId, cancellationToken);
        await emailNotifier.RecurringBookingAdded(notification.EntityId, cancellationToken);
    }
}
