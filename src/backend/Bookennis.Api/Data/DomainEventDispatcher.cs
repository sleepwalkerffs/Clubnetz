
namespace Bookennis.Api.Data;

public class DomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    public Task Dispatch(INotification notification, CancellationToken cancellationToken)
        => mediator.Publish(notification, cancellationToken);
}