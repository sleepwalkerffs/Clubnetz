namespace Bookennis.Shared.Controller.Push;

public record GetPushConfigurationResult
{
    /// <summary>VAPID public key the browser subscribes with. <c>null</c> if push notifications are not set up on the server.</summary>
    public string? PublicKey { get; init; }
}

/// <summary>The push subscription of a browser (PushSubscription.toJSON()).</summary>
public record SavePushSubscriptionModel
{
    public string? Endpoint { get; init; }
    public string? P256dh { get; init; }
    public string? Auth { get; init; }
}

public record DeletePushSubscriptionModel
{
    public string? Endpoint { get; init; }
}
