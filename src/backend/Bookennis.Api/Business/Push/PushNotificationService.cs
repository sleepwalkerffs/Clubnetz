using System.Globalization;
using Bookennis.Api.Config;
using Bookennis.Api.Infrastructure;

namespace Bookennis.Api.Business.Push;

public interface IPushNotificationService
{
    /// <summary>
    /// Sends a notification to all devices of the recipients. The notification is created once per language.
    /// Delivery happens in a background job, so the caller is neither slowed down nor affected by failures.
    /// </summary>
    Task Notify(IReadOnlyCollection<PushRecipient> recipients, Func<CultureInfo, PushNotification> createNotification, CancellationToken cancellationToken);
}

public class PushNotificationService(IMediator mediator, AppSettings appSettings) : IPushNotificationService
{
    public async Task Notify(IReadOnlyCollection<PushRecipient> recipients, Func<CultureInfo, PushNotification> createNotification, CancellationToken cancellationToken)
    {
        if (recipients.Count == 0 || !appSettings.Push.IsConfigured)
            return;

        foreach (var language in recipients.GroupBy(r => r.Language))
        {
            var userIds = language.Select(r => r.UserId).Distinct().ToArray();
            await mediator.Send(new SendPushNotification(userIds, createNotification(language.Key.ToCultureInfo())), cancellationToken);
        }
    }
}
