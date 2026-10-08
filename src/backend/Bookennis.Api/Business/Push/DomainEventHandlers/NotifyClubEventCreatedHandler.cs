using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Base;
using Bookennis.Domain.ClubEvents;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push.DomainEventHandlers;

/// <summary>
/// Push notification and email for the club members when a new event is added to the club calendar,
/// unless the organizer chose not to notify them.
/// </summary>
public class NotifyClubEventCreatedHandler(AppDbContext context, IClubEventNotifier notifier, IUserAccessor userAccessor)
    : INotificationHandler<DomainEvent<EntityCreated<ClubEvent>>>
{
    public async Task Handle(DomainEvent<EntityCreated<ClubEvent>> notification, CancellationToken cancellationToken)
    {
        var clubEvent = await context.ClubEvents
            .IgnoreQueryFilters()
            .Where(e => e.Id == notification.EntityId)
            .Select(e => new { e.StartDate, e.NotifyMembers })
            .SingleOrDefaultAsync(cancellationToken);

        if (clubEvent is null || !clubEvent.NotifyMembers || clubEvent.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return;

        var creatorUserId = userAccessor.TryGetUserId(out var userId) ? userId : (int?)null;
        await notifier.EventCreated(notification.EntityId, creatorUserId, cancellationToken);
    }
}
