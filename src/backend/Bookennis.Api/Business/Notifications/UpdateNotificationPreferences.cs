using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Notifications;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Notifications;

/// <summary>Saves the notification channels of the current user. Types that are not part of the request keep their channels.</summary>
public record UpdateNotificationPreferences(IReadOnlyCollection<UpdateNotificationPreferences.Preference> Preferences) : ICommand
{
    public enum ErrorCode
    {
        InvalidNotificationType = 0
    }

    public record Preference(NotificationType Type, bool Push, bool Email);

    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<UpdateNotificationPreferences>
    {
        public async Task<Unit> Handle(UpdateNotificationPreferences request, CancellationToken cancellationToken)
        {
            var userId = userAccessor.GetUserId();

            if (request.Preferences.Any(p => !Enum.IsDefined(p.Type)))
                throw new PreconditionException(ErrorCode.InvalidNotificationType, "Unknown notification type.");

            var saved = await context.NotificationPreferences
                .Where(p => p.UserId == userId)
                .ToDictionaryAsync(p => p.Type, cancellationToken);

            // The last entry of a type wins
            foreach (var preference in request.Preferences.GroupBy(p => p.Type).Select(g => g.Last()))
            {
                if (saved.TryGetValue(preference.Type, out var existing))
                    existing.Update(preference.Push, preference.Email);
                else
                    context.NotificationPreferences.Add(new NotificationPreference(userId, preference.Type, preference.Push, preference.Email));
            }

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
