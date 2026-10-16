using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Memberships;
using Fusonic.Extensions.Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// Acts on an event Stripe reported (stored by <see cref="ReceiveStripeWebhook"/>). The payload of the webhook is not trusted:
/// the object is loaded again from Stripe with the account the event came from.
/// </summary>
public record ProcessStripeEvent(int StripeWebhookEventId) : ICommand
{
    public const string CheckoutCompleted = "checkout.session.completed";
    public const string CheckoutAsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
    public const string CheckoutAsyncPaymentFailed = "checkout.session.async_payment_failed";
    public const string CheckoutExpired = "checkout.session.expired";
    public const string ChargeRefunded = "charge.refunded";
    public const string DisputeCreated = "charge.dispute.created";
    public const string AccountUpdated = "account.updated";
    public const string AccountDeauthorized = "account.application.deauthorized";

    public static readonly IReadOnlySet<string> HandledTypes = new HashSet<string>
    {
        CheckoutCompleted, CheckoutAsyncPaymentSucceeded, CheckoutAsyncPaymentFailed, CheckoutExpired,
        ChargeRefunded, DisputeCreated, AccountUpdated, AccountDeauthorized,
    };

    // Runs as a background job, so Stripe gets its answer right away and a failure is retried by Hangfire
    [OutOfBand]
    public class Handler(
        AppDbContext context,
        IStripeGateway gateway,
        IMembershipFulfillment fulfillment,
        IMembershipSettlement settlement,
        IMembershipNotifier notifier,
        ICourtPurchaseSettlement courtSettlement,
        ICourtPurchaseNotifier courtNotifier,
        ILogger<ProcessStripeEvent> logger) : IRequestHandler<ProcessStripeEvent>
    {
        public async Task<Unit> Handle(ProcessStripeEvent request, CancellationToken cancellationToken)
        {
            var stripeEvent = await context.StripeWebhookEvents.FirstOrDefaultAsync(e => e.Id == request.StripeWebhookEventId, cancellationToken);
            if (stripeEvent is null || stripeEvent.ProcessedAt is not null)
                return default;

            // Background jobs have no tenant. The club is the one the Stripe account of the event belongs to - nothing else decides it.
            var account = stripeEvent.StripeAccountId is null
                ? null
                : await context.ClubPaymentAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.StripeAccountId == stripeEvent.StripeAccountId, cancellationToken);

            // What has to be mailed is collected and sent after the changes are saved
            var emails = new List<Func<Task>>();

            if (account is null)
                logger.LogInformation("Stripe event {EventId} ({Type}) belongs to no connected club and is ignored.", stripeEvent.EventId, stripeEvent.Type);
            else
                await Handle(stripeEvent, account, emails, cancellationToken);

            stripeEvent.MarkProcessed(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var send in emails)
                await send();

            return default;
        }

        private async Task Handle(StripeWebhookEvent stripeEvent, ClubPaymentAccount account, List<Func<Task>> emails, CancellationToken cancellationToken)
        {
            switch (stripeEvent.Type)
            {
                case CheckoutCompleted:
                case CheckoutAsyncPaymentSucceeded:
                case CheckoutAsyncPaymentFailed:
                case CheckoutExpired:
                    await HandleCheckout(stripeEvent, account, emails, cancellationToken);
                    break;

                case ChargeRefunded:
                {
                    var charge = await gateway.GetCharge(account.StripeAccountId, stripeEvent.ObjectId, cancellationToken);
                    if (charge is { IsFullyRefunded: true } && await FindPurchaseByPayment(account, charge.PaymentIntentId, cancellationToken) is { } refundedPurchase)
                    {
                        await HandleCourtPurchaseRefund(refundedPurchase, emails, cancellationToken);
                    }
                    else if (charge is { IsFullyRefunded: true }
                     && await FindOrderByPayment(account, charge.PaymentIntentId, cancellationToken) is { } order
                     && order.Status is MembershipOrderStatus.Paid or MembershipOrderStatus.Processing or MembershipOrderStatus.Disputed or MembershipOrderStatus.Cancelled)
                    {
                        var wasCancelled = order.Status == MembershipOrderStatus.Cancelled;
                        order.MarkRefunded();

                        // A cancelled order was refunded by the treasurer on purpose, there is nothing to look at
                        if (!wasCancelled)
                            emails.Add(() => notifier.NeedsAttention(order, MembershipAttentionReason.Refunded, cancellationToken));
                    }

                    break;
                }

                case DisputeCreated:
                {
                    var dispute = await gateway.GetDispute(account.StripeAccountId, stripeEvent.ObjectId, cancellationToken);
                    if (dispute is not null && await FindPurchaseByPayment(account, dispute.PaymentIntentId, cancellationToken) is { } disputedPurchase)
                    {
                        if (disputedPurchase.Status is CourtPurchaseStatus.Paid or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Cancelled or CourtPurchaseStatus.Compensated)
                        {
                            var wasCompensated = disputedPurchase.Status == CourtPurchaseStatus.Compensated;
                            disputedPurchase.MarkDisputed();
                            if (wasCompensated)
                                await TakeBackCompensation(disputedPurchase, emails, cancellationToken);
                            emails.Add(() => courtNotifier.NeedsAttention(disputedPurchase, CourtPurchaseAttentionReason.Disputed, cancellationToken));
                        }
                    }
                    else if (dispute is not null
                     && await FindOrderByPayment(account, dispute.PaymentIntentId, cancellationToken) is { } order
                     && order.Status is MembershipOrderStatus.Paid or MembershipOrderStatus.Processing)
                    {
                        order.MarkDisputed();
                        emails.Add(() => notifier.NeedsAttention(order, MembershipAttentionReason.Disputed, cancellationToken));
                    }

                    break;
                }

                case AccountUpdated:
                {
                    var info = await gateway.GetAccount(account.StripeAccountId, cancellationToken);
                    if (info is not null)
                        account.UpdateStatus(info.ChargesEnabled, info.PayoutsEnabled, info.DetailsSubmitted);

                    break;
                }

                case AccountDeauthorized:
                    // The club took the access away in its Stripe dashboard. Stripe would refuse every call for the account from now on.
                    if (await gateway.GetAccount(account.StripeAccountId, cancellationToken) is null)
                        context.ClubPaymentAccounts.Remove(account);

                    break;
            }
        }

        private async Task HandleCheckout(StripeWebhookEvent stripeEvent, ClubPaymentAccount account, List<Func<Task>> emails, CancellationToken cancellationToken)
        {
            var session = await gateway.GetCheckoutSession(account.StripeAccountId, stripeEvent.ObjectId, cancellationToken);
            if (session is null || !StripeCheckoutReference.TryParse(session.ClientReferenceId, out var kind, out var orderId))
                return;

            if (kind == StripeCheckoutKind.CourtPurchase)
            {
                await HandleCourtPurchaseCheckout(stripeEvent, account, session, orderId, emails, cancellationToken);
                return;
            }

            // The order has to belong to the club of the Stripe account. An order id of another club in the session changes nothing.
            var order = await context.MembershipOrders
                .IgnoreQueryFilters()
                .Include(o => o.Members)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.ClubId == account.ClubId, cancellationToken);

            if (order is null)
            {
                logger.LogWarning("Stripe event {EventId}: the order {OrderId} does not exist in club {ClubId}.", stripeEvent.EventId, orderId, account.ClubId);
                return;
            }

            switch (stripeEvent.Type)
            {
                // Paid by card, or a SEPA debit that settles in a few days: the members are enrolled now
                case CheckoutCompleted:
                    await settlement.SettleCompletedSession(order, session, emails, cancellationToken);
                    break;

                case CheckoutAsyncPaymentSucceeded:
                    await settlement.Settle(order, () => order.MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow, paymentIntentId: session.PaymentIntentId), emails, cancellationToken);
                    break;

                // The money is not coming: the members are no longer enrolled by this order. Paying again enrolls them again.
                case CheckoutAsyncPaymentFailed:
                    if (order.Status is MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Processing)
                    {
                        order.MarkFailed();
                        await fulfillment.Revoke(order, cancellationToken);
                        emails.Add(() => notifier.PaymentFailed(order, cancellationToken));
                        emails.Add(() => notifier.NeedsAttention(order, MembershipAttentionReason.PaymentFailed, cancellationToken));
                    }

                    break;

                case CheckoutExpired:
                    order.CheckoutExpired(session.Id);
                    break;
            }
        }

        private async Task HandleCourtPurchaseCheckout(StripeWebhookEvent stripeEvent, ClubPaymentAccount account, StripeCheckoutSession session, int purchaseId, List<Func<Task>> emails, CancellationToken cancellationToken)
        {
            // The purchase has to belong to the club of the Stripe account. A purchase id of another club in the session changes nothing.
            var purchase = await context.CourtPurchases
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == purchaseId && p.ClubId == account.ClubId, cancellationToken);

            if (purchase is null)
            {
                logger.LogWarning("Stripe event {EventId}: the court purchase {PurchaseId} does not exist in club {ClubId}.", stripeEvent.EventId, purchaseId, account.ClubId);
                return;
            }

            switch (stripeEvent.Type)
            {
                case CheckoutCompleted:
                    await courtSettlement.SettleCompletedSession(purchase, session, emails, cancellationToken);
                    break;

                case CheckoutAsyncPaymentSucceeded:
                    await courtSettlement.Settle(purchase, paid: true, session.PaymentIntentId, emails, cancellationToken);
                    break;

                // The money is not coming: the slot is given free again (when it has not been played yet)
                case CheckoutAsyncPaymentFailed:
                    if (purchase.Status is CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Processing)
                    {
                        purchase.MarkFailed();
                        await courtSettlement.RemoveBooking(purchase, onlyUpcoming: true, cancellationToken);
                        emails.Add(() => courtNotifier.PaymentFailed(purchase, cancellationToken));
                        emails.Add(() => courtNotifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.PaymentFailed, cancellationToken));
                    }

                    break;

                // Only the payment page the purchase waits for releases the hold
                case CheckoutExpired:
                    if (purchase.Status == CourtPurchaseStatus.AwaitingPayment && purchase.StripeCheckoutSessionId == session.Id)
                        await courtSettlement.ReleaseHold(purchase, cancellationToken);

                    break;
            }
        }

        private async Task HandleCourtPurchaseRefund(CourtPurchase purchase, List<Func<Task>> emails, CancellationToken cancellationToken)
        {
            if (purchase.Status is not (CourtPurchaseStatus.Paid or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Disputed or CourtPurchaseStatus.Cancelled or CourtPurchaseStatus.Compensated))
                return;

            // A cancelled or compensated purchase was refunded by the treasurer on purpose, there is nothing to look at
            var wasCompensated = purchase.Status == CourtPurchaseStatus.Compensated;
            var onPurpose = wasCompensated || purchase.Status == CourtPurchaseStatus.Cancelled;
            purchase.MarkRefunded();
            await courtSettlement.RemoveBooking(purchase, onlyUpcoming: true, cancellationToken);

            if (wasCompensated)
                await TakeBackCompensation(purchase, emails, cancellationToken);
            if (!onPurpose)
                emails.Add(() => courtNotifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.Refunded, cancellationToken));
        }

        /// <summary>
        /// The money of a compensated purchase goes back to the buyer: the credits granted for the removed booking are taken back,
        /// as far as the buyer still has them. The treasurers are told about the rest.
        /// </summary>
        private async Task TakeBackCompensation(CourtPurchase purchase, List<Func<Task>> emails, CancellationToken cancellationToken)
        {
            if (purchase is not { GrantedCompensationCredits: > 0, MemberId: { } buyerId })
                return;

            await context.LockCourts(purchase.ClubId, cancellationToken);
            var balance = await context.GetCreditBalance(purchase.ClubId, buyerId, cancellationToken);
            var takeBack = Math.Min(purchase.GrantedCompensationCredits, Math.Max(0, balance));
            if (takeBack > 0)
                context.Add(new BookingCreditGrant(purchase.ClubId, buyerId, -takeBack, BookingCreditReason.BookingRemovedByClub, null, null));

            if (takeBack < purchase.GrantedCompensationCredits)
                emails.Add(() => courtNotifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.CompensationNotTakenBack, cancellationToken));
        }

        private async Task<CourtPurchase?> FindPurchaseByPayment(ClubPaymentAccount account, string? paymentIntentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(paymentIntentId))
                return null;

            return await context.CourtPurchases
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId && p.ClubId == account.ClubId, cancellationToken);
        }

        private async Task<MembershipOrder?> FindOrderByPayment(ClubPaymentAccount account, string? paymentIntentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(paymentIntentId))
                return null;

            return await context.MembershipOrders
                .IgnoreQueryFilters()
                .Include(o => o.Members)
                .FirstOrDefaultAsync(o => o.StripePaymentIntentId == paymentIntentId && o.ClubId == account.ClubId, cancellationToken);
        }
    }
}
