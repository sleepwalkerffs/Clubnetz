using Bookennis.Domain.Base;

namespace Bookennis.Api.Business.Events;

public record DomainEvent<TEvent> : INotification where TEvent : IDomainEvent
{
    public Guid EntityTypeGuid { get; }
    public int EntityId { get; }
    public TEvent Event { get; }

    public DomainEvent(Guid entityTypeGuid, int entityId, TEvent @event)
    {
        EntityTypeGuid = entityTypeGuid;
        EntityId = entityId;
        Event = @event;
    }
}
