
namespace Bookennis.Api.Data;

public interface IDomainEventDispatcher
{
    Task Dispatch(INotification notification, CancellationToken cancellationToken);
}