namespace Bookennis.Api.Config;

/// <summary>
/// Stripe keys of this installation (the Connect platform the clubs connect their own Stripe accounts to).
/// Online payment of membership fees is switched off everywhere as long as they are not configured:
/// environment variables <c>Stripe__SecretKey</c> and <c>Stripe__WebhookSecret</c>.
/// </summary>
public class StripeSettings
{
    /// <summary>Secret key of the platform account (<c>sk_live_...</c> or <c>sk_test_...</c>). Secret.</summary>
    public string? SecretKey { get; set; }

    /// <summary>Signing secret of the webhook endpoint <c>api/Stripe/Webhook</c> (<c>whsec_...</c>). Secret.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Share of every payment that goes to the platform, in percent. 0 = the club gets everything (minus Stripe's own fees).
    /// </summary>
    public decimal ApplicationFeePercent { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);
}
