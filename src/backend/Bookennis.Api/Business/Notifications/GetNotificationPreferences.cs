using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Notifications;
using Bookennis.Shared.Controller.Notifications;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;
using NotificationType = Bookennis.Domain.Notifications.NotificationType;

namespace Bookennis.Api.Business.Notifications;

/// <summary>The notification channels of the current user, for every notification type (defaults where nothing was saved).</summary>
public record GetNotificationPreferences : IQuery<GetNotificationPreferencesResult>
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<GetNotificationPreferences, GetNotificationPreferencesResult>
    {
        public async Task<GetNotificationPreferencesResult> Handle(GetNotificationPreferences request, CancellationToken cancellationToken)
        {
            var userId = userAccessor.GetUserId();

            var saved = await context.NotificationPreferences
                .Where(p => p.UserId == userId)
                .ToDictionaryAsync(p => p.Type, cancellationToken);

            return new GetNotificationPreferencesResult
            {
                Preferences = Enum.GetValues<NotificationType>()
                    .Select(type =>
                    {
                        var preference = saved.GetValueOrDefault(type);
                        return new NotificationPreferenceDto
                        {
                            Type = (Bookennis.Shared.Controller.Notifications.NotificationType)(int)type,
                            Push = preference?.Push ?? NotificationPreference.DefaultPush,
                            Email = preference?.Email ?? NotificationPreference.DefaultEmail
                        };
                    })
                    .ToList()
            };
        }
    }
}
