namespace Bookennis.Domain.Memberships;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Memberships.MembershipPaymentMethod</c>.</summary>
public enum MembershipPaymentMethod
{
    /// <summary>Paid with Stripe Checkout.</summary>
    Online = 0,
    BankTransfer = 1,
    Cash = 2,
    Other = 3,
}
