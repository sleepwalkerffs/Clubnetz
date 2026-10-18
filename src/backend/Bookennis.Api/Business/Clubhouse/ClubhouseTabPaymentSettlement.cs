using Bookennis.Api.Business.Clubhouse.SumUp;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubhouse;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubhouse;

public enum ClubhouseTabPaymentCheck
{
    /// <summary>SumUp could not be asked (not reachable, account disconnected or to be connected again) or answered nonsense: nothing changed.</summary>
    Unknown,

    /// <summary>The payment page is still open.</summary>
    Pending,
    Paid,

    /// <summary>The payment page closed without money; the tab is open again.</summary>
    Expired,
}

public interface IClubhouseTabPaymentSettlement
{
    /// <summary>
    /// Asks SumUp how the checkout of the payment stands and books the answer: paid, or expired (the tab is unlocked). With
    /// <paramref name="closeWhenUnpaid"/> a checkout that is still open is closed at SumUp and the payment expired (the payment page ran
    /// out). The emails to send are added to <paramref name="emails"/>: send them after the changes are saved.
    /// </summary>
    Task<ClubhouseTabPaymentCheck> Check(ClubhouseTabPayment payment, bool closeWhenUnpaid, List<Func<Task>> emails, CancellationToken cancellationToken);
}

/// <summary>
/// The one place where an online payment of a tab is booked, for every way the app learns about it: the webhook of SumUp
/// (<see cref="CheckClubhouseTabPayment"/>), the member coming back (<see cref="GetClubhouseTabPayment"/>) and the job that closes
/// payment pages that ran out. It never trusts what it is told: it asks SumUp with the account of the host and checks that the
/// checkout is the one of the payment (id, reference, merchant, amount). A second arrival finds the payment settled and does nothing.
/// Money is never lost silently: a payment that can't be booked on its tab any more is reported to the host.
/// </summary>
public class ClubhouseTabPaymentSettlement(
    AppDbContext context,
    ISumUpGateway gateway,
    ISumUpAccessTokens tokens,
    IClubhouseTabPaymentNotifier notifier,
    ILogger<ClubhouseTabPaymentSettlement> logger) : IClubhouseTabPaymentSettlement
{
    public async Task<ClubhouseTabPaymentCheck> Check(ClubhouseTabPayment payment, bool closeWhenUnpaid, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        if (payment.Status == ClubhouseTabPaymentStatus.Paid)
            return ClubhouseTabPaymentCheck.Paid;

        if (!gateway.IsConfigured || payment.CheckoutId is not { } checkoutId)
            return ClubhouseTabPaymentCheck.Unknown;

        // Also runs in background jobs without a tenant: the club of the payment is checked explicitly
        var account = await context.ClubhouseSumUpAccounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.ClubId == payment.ClubId, cancellationToken);

        // The checkout lives on the merchant account it was created on: another account can't tell anything about it
        if (account is null || account.MerchantCode != payment.MerchantCode)
            return await CouldNotVerify(payment, closeWhenUnpaid, emails, cancellationToken);

        SumUpCheckout? checkout;
        try
        {
            var accessToken = await tokens.Get(account, cancellationToken);
            checkout = await gateway.GetCheckout(accessToken, checkoutId, cancellationToken);

            if (checkout is { IsPaid: false, IsExpired: false } && closeWhenUnpaid)
            {
                // Closed first, then asked again: a payment made in the last moment is not lost
                await gateway.DeactivateCheckout(accessToken, checkoutId, cancellationToken);
                checkout = await gateway.GetCheckout(accessToken, checkoutId, cancellationToken);
            }
        }
        catch (SumUpAuthorizationException e)
        {
            logger.LogWarning(e, "The payment {PaymentId} of a clubhouse tab could not be checked: the SumUp account has to be connected again.", payment.Id);
            return await CouldNotVerify(payment, closeWhenUnpaid, emails, cancellationToken);
        }
        catch (SumUpException e)
        {
            logger.LogWarning(e, "The payment {PaymentId} of a clubhouse tab could not be checked with SumUp.", payment.Id);
            return ClubhouseTabPaymentCheck.Unknown;
        }

        // A checkout that does not exist (any more) or is not the one of this payment proves nothing
        if (checkout is null || !Matches(payment, checkout))
        {
            logger.LogWarning("SumUp answered for the payment {PaymentId} of a clubhouse tab with a checkout that does not match it.", payment.Id);
            return closeWhenUnpaid ? await CouldNotVerify(payment, closeWhenUnpaid, emails, cancellationToken) : ClubhouseTabPaymentCheck.Unknown;
        }

        if (checkout.IsPaid)
        {
            await Settle(payment, checkout.TransactionCode, emails, cancellationToken);
            return ClubhouseTabPaymentCheck.Paid;
        }

        if (checkout.IsExpired || closeWhenUnpaid)
        {
            await Expire(payment, cancellationToken);
            return ClubhouseTabPaymentCheck.Expired;
        }

        // PENDING, or FAILED: the last attempt failed, the member can try again on the same page
        return payment.IsPending ? ClubhouseTabPaymentCheck.Pending : ClubhouseTabPaymentCheck.Expired;
    }

    private static bool Matches(ClubhouseTabPayment payment, SumUpCheckout checkout)
        => checkout.Id == payment.CheckoutId
           && checkout.Reference == payment.CheckoutReference
           && (checkout.MerchantCode is null || checkout.MerchantCode == payment.MerchantCode)
           && checkout.AmountCents == payment.AmountCents
           && string.Equals(checkout.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase);

    private async Task Settle(ClubhouseTabPayment payment, string? transactionCode, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        payment.MarkPaid(transactionCode, DateTimeOffset.UtcNow);

        await context.LockClubhouseTab(payment.TabId, cancellationToken);
        var tab = await context.ClubhouseTabs.IgnoreQueryFilters()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == payment.TabId && t.ClubId == payment.ClubId, cancellationToken);

        emails.Add(() => notifier.Paid(payment, cancellationToken));

        if (tab is null)
        {
            emails.Add(() => notifier.NeedsAttention(payment, ClubhouseTabPaymentAttentionReason.TabChanged, cancellationToken));
            return;
        }

        // Locked for this payment, or the money of a payment page that ran out arrived late and the tab is still what was paid
        var isForThisTab = tab.Status == ClubhouseTabStatus.AwaitingPayment || (tab.IsOpen && tab.TotalCents == payment.AmountCents);
        if (!isForThisTab)
        {
            logger.LogWarning("The payment {PaymentId} arrived for clubhouse tab {TabId} in status {Status}.", payment.Id, tab.Id, tab.Status);
            var reason = tab.IsOpen ? ClubhouseTabPaymentAttentionReason.TabChanged : ClubhouseTabPaymentAttentionReason.PaidTwice;
            emails.Add(() => notifier.NeedsAttention(payment, reason, cancellationToken));
            return;
        }

        tab.MarkPaidOnline(payment.PaidAt!.Value);
    }

    private async Task Expire(ClubhouseTabPayment payment, CancellationToken cancellationToken)
    {
        // A payment that expired earlier no longer holds the lock: a newer payment page may hold it now
        if (!payment.IsPending)
            return;

        payment.Expire();

        await context.LockClubhouseTab(payment.TabId, cancellationToken);
        var tab = await context.ClubhouseTabs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == payment.TabId && t.ClubId == payment.ClubId, cancellationToken);

        // There is one payment page per tab at a time (unique index), so the lock was this one's
        tab?.EndOnlinePayment();
    }

    /// <summary>
    /// SumUp can't be asked with the account of the payment (disconnected, connected again with another account, refused). While the
    /// payment page may still be used nothing changes; once it ran out the tab is unlocked and the host checks the SumUp dashboard.
    /// </summary>
    private async Task<ClubhouseTabPaymentCheck> CouldNotVerify(ClubhouseTabPayment payment, bool closeWhenUnpaid, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        if (!closeWhenUnpaid || !payment.IsPending)
            return ClubhouseTabPaymentCheck.Unknown;

        await Expire(payment, cancellationToken);
        emails.Add(() => notifier.NeedsAttention(payment, ClubhouseTabPaymentAttentionReason.CouldNotVerify, cancellationToken));
        return ClubhouseTabPaymentCheck.Expired;
    }
}
