using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push;

/// <summary>
/// Registers the device of the current user for push notifications. Called when the user switches them on and
/// on every app start, because browsers replace subscriptions from time to time.
/// </summary>
public record SavePushSubscription(string? Endpoint, string? P256dh, string? Auth) : ICommand
{
    public const int MaxDevicesPerUser = 10;

    public enum ErrorCode
    {
        InvalidPushSubscription = 0,
        PushNotificationsNotAvailable = 1,
    }

    public class Handler(AppDbContext context, IUserAccessor userAccessor, AppSettings appSettings) : IRequestHandler<SavePushSubscription>
    {
        public async Task<Unit> Handle(SavePushSubscription request, CancellationToken cancellationToken)
        {
            var userId = userAccessor.GetUserId();

            if (!appSettings.Push.IsConfigured)
                throw new PreconditionException(ErrorCode.PushNotificationsNotAvailable, "Push notifications are not configured");

            if (!PushEndpoint.IsAllowed(request.Endpoint)
                || request.Endpoint!.Length > PushSubscription.MaxEndpointLength
                || string.IsNullOrWhiteSpace(request.P256dh) || request.P256dh.Length > PushSubscription.MaxKeyLength
                || string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > PushSubscription.MaxKeyLength)
            {
                throw new PreconditionException(ErrorCode.InvalidPushSubscription, "Invalid push subscription");
            }

            var subscription = await context.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);
            if (subscription is null)
            {
                context.PushSubscriptions.Add(new PushSubscription(userId, request.Endpoint, request.P256dh, request.Auth));

                // Keeps the list from growing forever when devices are replaced without signing out
                var oldest = await context.PushSubscriptions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.Id)
                    .Skip(MaxDevicesPerUser - 1)
                    .ToListAsync(cancellationToken);
                context.PushSubscriptions.RemoveRange(oldest);
            }
            else if (subscription.UserId != userId || subscription.P256dh != request.P256dh || subscription.Auth != request.Auth)
            {
                subscription.Update(userId, request.P256dh, request.Auth);
            }

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
