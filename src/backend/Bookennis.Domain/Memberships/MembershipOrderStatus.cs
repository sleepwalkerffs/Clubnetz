namespace Bookennis.Domain.Memberships;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Memberships.MembershipOrderStatus</c>.</summary>
public enum MembershipOrderStatus
{
    /// <summary>The treasurer has to confirm that the member may have the plan.</summary>
    AwaitingApproval = 0,
    AwaitingPayment = 1,

    /// <summary>A SEPA direct debit was started and has not settled yet. The members are already enrolled.</summary>
    Processing = 2,
    Paid = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6,
    Disputed = 7,
}
