namespace Bookennis.Api.Config;

/// <summary>
/// Web Push (VAPID) settings. Push notifications are switched off as long as no key pair is configured.
/// Create a key pair with scripts/New-VapidKeys.ps1 and never change it afterwards: existing
/// subscriptions are bound to the public key and would stop working.
/// </summary>
public class PushSettings
{
    /// <summary>VAPID public key (base64url, uncompressed P-256 point). Handed out to the browsers.</summary>
    public string? PublicKey { get; set; }

    /// <summary>VAPID private key (base64url). Secret.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>Contact of the operator for the push services, a "mailto:" or "https:" uri.</summary>
    public string? Subject { get; set; }

    /// <summary>How long before a booking starts its players are reminded.</summary>
    public int BookingReminderLeadMinutes { get; set; } = 120;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey) && !string.IsNullOrWhiteSpace(Subject);
}
