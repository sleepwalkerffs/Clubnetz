using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push;

/// <summary>Stops push notifications on a device of the current user (switched off, or signed out on that device).</summary>
public record DeletePushSubscription(string? Endpoint) : ICommand
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<DeletePushSubscription>
    {
        public async Task<Unit> Handle(DeletePushSubscription request, CancellationToken cancellationToken)
        {
            var userId = userAccessor.GetUserId();

            var subscription = await context.PushSubscriptions
                .SingleOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UserId == userId, cancellationToken);

            if (subscription is not null)
            {
                context.PushSubscriptions.Remove(subscription);
                await context.SaveChangesAsync(cancellationToken);
            }

            return default;
        }
    }
}
