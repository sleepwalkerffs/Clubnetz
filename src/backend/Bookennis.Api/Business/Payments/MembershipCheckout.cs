using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

public interface IMembershipCheckout
{
    /// <summary>
    /// Closes the payment page of an order that was cancelled or paid another way, so it can't take money any more.
    /// Best effort: when Stripe can't be reached the page runs out by itself, and a payment that still arrives is reported to the treasurers.
    /// </summary>
    Task ExpireSession(int clubId, string checkoutSessionId, CancellationToken cancellationToken);
}

public class MembershipCheckout(AppDbContext context, IStripeGateway gateway, ILogger<MembershipCheckout> logger) : IMembershipCheckout
{
    public async Task ExpireSession(int clubId, string checkoutSessionId, CancellationToken cancellationToken)
    {
        if (!gateway.IsConfigured)
            return;

        var accountId = await context.ClubPaymentAccounts
            .Where(a => a.ClubId == clubId)
            .Select(a => a.StripeAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (accountId is null)
            return;

        try
        {
            await gateway.ExpireCheckoutSession(accountId, checkoutSessionId, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "The checkout session {CheckoutSessionId} of club {ClubId} could not be closed.", checkoutSessionId, clubId);
        }
    }
}
