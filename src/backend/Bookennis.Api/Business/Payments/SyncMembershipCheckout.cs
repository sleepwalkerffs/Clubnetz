using System.Globalization;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// The member came back from the payment page: asks Stripe how the payment page of the order ended and books it, without waiting
/// for the webhook (Stripe recommends doing both). Only what Stripe answers counts, never what the browser says.
/// Best effort: when Stripe can't be reached, the webhook books the payment later.
/// </summary>
public record SyncMembershipCheckout(int ClubId, int MembershipOrderId) : ICommand
{
    public const string SessionComplete = "complete";

    public class Handler(AppDbContext context, IStripeGateway gateway, IMembershipSettlement settlement, ILogger<SyncMembershipCheckout> logger)
        : IRequestHandler<SyncMembershipCheckout>
    {
        public async Task<Unit> Handle(SyncMembershipCheckout request, CancellationToken cancellationToken)
        {
            if (!gateway.IsConfigured)
                return default;

            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);

            // A failed debit clears the session, so only a payment page that was opened again is asked about
            if (order.StripeCheckoutSessionId is not { } sessionId || order.Status is not (MembershipOrderStatus.AwaitingPayment or MembershipOrderStatus.Failed))
                return default;

            var accountId = await context.ClubPaymentAccounts.Where(a => a.ClubId == order.ClubId).Select(a => a.StripeAccountId).FirstOrDefaultAsync(cancellationToken);
            if (accountId is null)
                return default;

            try
            {
                var session = await gateway.GetCheckoutSession(accountId, sessionId, cancellationToken);
                if (session is not { Status: SessionComplete } || session.ClientReferenceId != order.Id.ToString(CultureInfo.InvariantCulture))
                    return default;

                var emails = new List<Func<Task>>();
                await settlement.SettleCompletedSession(order, session, emails, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                foreach (var send in emails)
                    await send();
            }
            catch (DbUpdateConcurrencyException)
            {
                // The webhook booked the payment at the same moment
                context.ChangeTracker.Clear();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "The payment page of order {OrderId} could not be checked with Stripe; the webhook books it.", order.Id);
                context.ChangeTracker.Clear();
            }

            return default;
        }
    }
}
