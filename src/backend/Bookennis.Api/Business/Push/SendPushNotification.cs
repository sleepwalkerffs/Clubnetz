using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push;

/// <summary>
/// Delivers a notification to all devices of the users. Use <see cref="IPushNotificationService"/> to send
/// notifications, it takes care of the recipients' languages.
/// </summary>
public record SendPushNotification(int[] UserIds, PushNotification Notification) : ICommand
{
    // Runs as a background job: the push services are slow compared to a request and may be unreachable
    [OutOfBand]
    public class Handler(AppDbContext context, IPushSender sender) : IRequestHandler<SendPushNotification>
    {
        public async Task<Unit> Handle(SendPushNotification request, CancellationToken cancellationToken)
        {
            var subscriptions = await context.PushSubscriptions
                .Where(s => request.UserIds.Contains(s.UserId))
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                var result = await sender.Send(subscription, request.Notification, cancellationToken);
                if (result == PushSendResult.SubscriptionGone)
                    context.PushSubscriptions.Remove(subscription);
            }

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
