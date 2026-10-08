using System.Net;
using System.Text.Json;
using Bookennis.Api.Config;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using PushSubscription = Bookennis.Domain.User.PushSubscription;

namespace Bookennis.Api.Business.Push;

public enum PushSendResult
{
    Sent,

    /// <summary>The push service doesn't know the subscription any more (app removed, permission revoked). Delete it.</summary>
    SubscriptionGone,

    Failed,
}

/// <summary>Delivers a notification to one device via the push service of its browser vendor.</summary>
public interface IPushSender
{
    Task<PushSendResult> Send(PushSubscription subscription, PushNotification notification, CancellationToken cancellationToken);
}

public sealed class WebPushSender : IPushSender, IDisposable
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly PushServiceClient? client;
    private readonly VapidAuthentication? authentication;
    private readonly ILogger<WebPushSender> logger;

    public WebPushSender(AppSettings appSettings, ILogger<WebPushSender> logger)
    {
        this.logger = logger;

        var settings = appSettings.Push;
        if (!settings.IsConfigured)
            return;

        authentication = new VapidAuthentication(settings.PublicKey, settings.PrivateKey) { Subject = settings.Subject };
        client = new PushServiceClient(httpClient)
        {
            DefaultAuthentication = authentication,
            // A notification that could not be delivered within half a day is not worth showing any more
            DefaultTimeToLive = (int)TimeSpan.FromHours(12).TotalSeconds,
        };
    }

    public async Task<PushSendResult> Send(PushSubscription subscription, PushNotification notification, CancellationToken cancellationToken)
    {
        if (client is null)
            return PushSendResult.Failed;

        try
        {
            var target = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = subscription.Endpoint };
            target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
            target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);

            var message = new PushMessage(JsonSerializer.Serialize(notification, PayloadJsonOptions))
            {
                // Wakes the device up right away, the notifications are all meant to be seen
                Urgency = PushMessageUrgency.High,
            };

            await client.RequestPushMessageDeliveryAsync(target, message, cancellationToken);
            return PushSendResult.Sent;
        }
        catch (PushServiceClientException e) when (e.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendResult.SubscriptionGone;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Best effort: one device that can't be reached must not keep the others from getting the notification
            logger.LogWarning(e, "Push notification to subscription {SubscriptionId} of user {UserId} could not be delivered.", subscription.Id, subscription.UserId);
            return PushSendResult.Failed;
        }
    }

    public void Dispose()
    {
        authentication?.Dispose();
        httpClient.Dispose();
    }
}
