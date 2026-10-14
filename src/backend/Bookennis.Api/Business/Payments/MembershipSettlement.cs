using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Memberships;

namespace Bookennis.Api.Business.Payments;

public interface IMembershipSettlement
{
    /// <summary>
    /// Books a payment (or a started debit) on the order with <paramref name="book"/> and enrolls its members, once.
    /// The emails to send are added to <paramref name="emails"/>: send them after the changes are saved.
    /// </summary>
    Task Settle(MembershipOrder order, Action book, List<Func<Task>> emails, CancellationToken cancellationToken);
}

/// <summary>
/// The one place where an online payment is booked on an order, for every way the app learns about it:
/// the webhook (<see cref="ProcessStripeEvent"/>) and the member coming back from the payment page (<see cref="SyncMembershipCheckout"/>).
/// Both can arrive for the same payment; the second one finds the order settled and does nothing.
/// </summary>
public class MembershipSettlement(IMembershipFulfillment fulfillment, IMembershipNotifier notifier) : IMembershipSettlement
{
    public async Task Settle(MembershipOrder order, Action book, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        // Money arrived for an order that can't take it any more, e.g. it was cancelled while the payment page was open
        if (!order.Status.IsSettledOrSettling() && !order.CanBeMarkedPaid)
        {
            emails.Add(() => notifier.NeedsAttention(order, MembershipAttentionReason.UnexpectedPayment, cancellationToken));
            return;
        }

        var wasSettled = order.Status.IsSettledOrSettling();
        var wasPaidAnotherWay = order.Status == MembershipOrderStatus.Paid && order.PaymentMethod != MembershipPaymentMethod.Online;
        book();

        // The treasurer already recorded the fee as paid by cash or transfer: it may have been paid twice
        if (wasPaidAnotherWay)
            emails.Add(() => notifier.NeedsAttention(order, MembershipAttentionReason.UnexpectedPayment, cancellationToken));

        if (wasSettled)
            return;

        await fulfillment.Complete(order, cancellationToken);
        emails.Add(() => notifier.PaymentReceived(order, cancellationToken));
    }
}

public static class MembershipSettlementExtensions
{
    /// <summary>
    /// A completed payment page: paid (card) is booked as paid, not paid yet (a SEPA debit that settles in a few days) as processing.
    /// The members are enrolled in both cases; a debit that fails later takes them out again.
    /// </summary>
    public static Task SettleCompletedSession(this IMembershipSettlement settlement, MembershipOrder order, StripeCheckoutSession session, List<Func<Task>> emails, CancellationToken cancellationToken)
        => session.IsPaid
            ? settlement.Settle(order, () => order.MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow, paymentIntentId: session.PaymentIntentId), emails, cancellationToken)
            : settlement.Settle(order, () => order.MarkProcessing(session.PaymentIntentId), emails, cancellationToken);
}
