using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Fusonic.Extensions.Common.Security;

namespace Bookennis.Api.Business.Push;

/// <summary>Sends a notification to the devices of the current user, so they can check that it arrives.</summary>
public record SendTestPushNotification : ICommand
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor, IPushNotificationService pushNotificationService) : IRequestHandler<SendTestPushNotification>
    {
        public async Task<Unit> Handle(SendTestPushNotification request, CancellationToken cancellationToken)
        {
            var recipients = await PushRecipients.ForUsers(context, type: null, [userAccessor.GetUserId()], exceptUserId: null, cancellationToken);

            await pushNotificationService.Notify(
                recipients,
                culture => new PushNotification(
                    PushTexts.Get(culture, "Test_Title"),
                    PushTexts.Get(culture, "Test_Body"),
                    "/account/profile",
                    Tag: "test"),
                cancellationToken);

            return default;
        }
    }
}
