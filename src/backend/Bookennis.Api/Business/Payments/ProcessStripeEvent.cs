using System.Globalization;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
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
        IMembershipNotifier notifier,
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
                    if (charge is { IsFullyRefunded: true }
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
                    if (dispute is not null
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
            if (session is null || !int.TryParse(session.ClientReferenceId, NumberStyles.None, CultureInfo.InvariantCulture, out var orderId))
                return;

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
                case CheckoutCompleted when session.IsPaid:
                case CheckoutAsyncPaymentSucceeded:
                    await Settle(order, emails, () => order.MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow, paymentIntentId: session.PaymentIntentId), cancellationToken);
                    break;

                // The member confirmed a SEPA debit that settles in a few days. They are enrolled now; a debit that fails is reported to the treasurers.
                case CheckoutCompleted:
                    await Settle(order, emails, () => order.MarkProcessing(session.PaymentIntentId), cancellationToken);
                    break;

                case CheckoutAsyncPaymentFailed:
                    if (order.Status is MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Processing)
                    {
                        order.MarkFailed();
                        emails.Add(() => notifier.PaymentFailed(order, cancellationToken));
                        emails.Add(() => notifier.NeedsAttention(order, MembershipAttentionReason.PaymentFailed, cancellationToken));
                    }

                    break;

                case CheckoutExpired:
                    order.CheckoutExpired(session.Id);
                    break;
            }
        }

        /// <summary>Books a payment (or a started debit) on the order and enrolls its members, once.</summary>
        private async Task Settle(MembershipOrder order, List<Func<Task>> emails, Action book, CancellationToken cancellationToken)
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
