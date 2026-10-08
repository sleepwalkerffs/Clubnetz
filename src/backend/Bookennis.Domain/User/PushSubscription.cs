using Bookennis.Domain.Base;

namespace Bookennis.Domain.User;

/// <summary>
/// A browser or installed app of a user that receives push notifications (Web Push).
/// The endpoint identifies the device at the browser vendor's push service, the keys encrypt the payload for it.
/// </summary>
public class PushSubscription : DomainEntity
{
    public const int MaxEndpointLength = 2000;
    public const int MaxKeyLength = 200;

#pragma warning disable CS8618
    private PushSubscription() { }
#pragma warning restore CS8618

    public PushSubscription(int userId, string endpoint, string p256dh, string auth)
    {
        UserId = userId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
    }

    public int UserId { get; private set; }
    public string Endpoint { get; private set; }
    public string P256dh { get; private set; }
    public string Auth { get; private set; }

    /// <summary>The device signed in with another account, or the browser rotated the keys.</summary>
    public void Update(int userId, string p256dh, string auth)
    {
        UserId = userId;
        P256dh = p256dh;
        Auth = auth;
    }
}
