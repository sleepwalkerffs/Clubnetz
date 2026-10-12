using Bookennis.Domain.Base;

namespace Bookennis.Domain.Memberships;

/// <summary>
/// The Stripe account of a club (Stripe Connect). The club is the merchant: payments go directly to this account.
/// Deleting the row disconnects the club; the account itself stays with Stripe.
/// </summary>
public class ClubPaymentAccount : TenantDomainEntity
{
    public const int MaxStripeAccountIdLength = 255;

#pragma warning disable CS8618
    private ClubPaymentAccount() { }
#pragma warning restore CS8618

    public ClubPaymentAccount(int clubId, string stripeAccountId, DateTimeOffset connectedAt)
    {
        ClubId = clubId;
        StripeAccountId = stripeAccountId;
        ConnectedAt = connectedAt;
    }

    public string StripeAccountId { get; private set; }

    /// <summary>The club can take payments. <c>false</c> until the onboarding at Stripe is finished.</summary>
    public bool ChargesEnabled { get; private set; }

    public bool PayoutsEnabled { get; private set; }
    public bool DetailsSubmitted { get; private set; }
    public DateTimeOffset ConnectedAt { get; private set; }

    public void UpdateStatus(bool chargesEnabled, bool payoutsEnabled, bool detailsSubmitted)
    {
        ChargesEnabled = chargesEnabled;
        PayoutsEnabled = payoutsEnabled;
        DetailsSubmitted = detailsSubmitted;
    }
}
