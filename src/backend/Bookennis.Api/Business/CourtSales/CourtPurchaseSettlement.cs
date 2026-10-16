using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.CourtSales;
using Bookennis.Global.Intervals;
using EntityFrameworkCore.Projectables.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtSales;

public interface ICourtPurchaseSettlement
{
    /// <summary>
    /// A completed payment page: paid (card) is booked as paid, not paid yet (a SEPA debit) as processing; the booking stays in both cases.
    /// The emails to send are added to <paramref name="emails"/>: send them after the changes are saved.
    /// </summary>
    Task SettleCompletedSession(CourtPurchase purchase, StripeCheckoutSession session, List<Func<Task>> emails, CancellationToken cancellationToken);

    /// <summary>Books an online payment (<paramref name="paid"/>) or a started debit on the purchase, once.</summary>
    Task Settle(CourtPurchase purchase, bool paid, string? paymentIntentId, List<Func<Task>> emails, CancellationToken cancellationToken);

    /// <summary>Nothing was paid: the hold is released and the booking deleted.</summary>
    Task ReleaseHold(CourtPurchase purchase, CancellationToken cancellationToken);

    /// <summary>Deletes the booking of the purchase; with <paramref name="onlyUpcoming"/> only when it has not started yet.</summary>
    Task RemoveBooking(CourtPurchase purchase, bool onlyUpcoming, CancellationToken cancellationToken);

    /// <summary>
    /// Asks Stripe how the payment page of a purchase that waits for its payment ended and books a completed one.
    /// Returns <c>false</c> when Stripe could not be asked or the page is still open (nothing was changed).
    /// </summary>
    Task<SessionCheck> CheckSession(CourtPurchase purchase, List<Func<Task>> emails, CancellationToken cancellationToken);
}

public enum SessionCheck
{
    /// <summary>No session, no Stripe or Stripe could not be reached: nothing is known.</summary>
    Unknown,
    Open,

    /// <summary>The session was completed and booked on the purchase.</summary>
    Settled,

    /// <summary>The session ran out (or was closed) without a payment.</summary>
    Expired,
}

/// <summary>
/// The one place where an online payment is booked on a court purchase, for every way the app learns about it:
/// the webhook (<see cref="ProcessStripeEvent"/>), the buyer coming back (<see cref="SyncCourtPurchaseCheckout"/>), releasing a hold
/// and the hold release job. A second arrival finds the purchase settled and does nothing. Money is never lost silently:
/// a payment that arrives for a purchase that can't take it any more is reported to the treasurers.
/// </summary>
public class CourtPurchaseSettlement(AppDbContext context, IStripeGateway gateway, ICourtPurchaseNotifier notifier, ILogger<CourtPurchaseSettlement> logger) : ICourtPurchaseSettlement
{
    public Task SettleCompletedSession(CourtPurchase purchase, StripeCheckoutSession session, List<Func<Task>> emails, CancellationToken cancellationToken)
        => Settle(purchase, session.IsPaid, session.PaymentIntentId, emails, cancellationToken);

    public async Task Settle(CourtPurchase purchase, bool paid, string? paymentIntentId, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        switch (purchase.Status)
        {
            case CourtPurchaseStatus.Paid:
                purchase.MarkPaid(DateTimeOffset.UtcNow, paymentIntentId);
                return;

            case CourtPurchaseStatus.Processing:
                // The debit started earlier and now settled: the buyer already got the confirmation
                if (paid)
                    purchase.MarkPaid(DateTimeOffset.UtcNow, paymentIntentId);
                else
                    purchase.MarkProcessing(paymentIntentId);
                return;

            case CourtPurchaseStatus.AwaitingPayment when purchase.BookingId is not null:
                Book(purchase, paid, paymentIntentId);
                emails.Add(() => notifier.Confirmed(purchase, cancellationToken));
                return;

            // The hold was released before the money arrived, or its booking was removed in the meantime (court blocked or set
            // inactive, overbooked): book the slot again if it is still free
            case CourtPurchaseStatus.AwaitingPayment:
            case CourtPurchaseStatus.Expired:
                await SettleLatePayment(purchase, paid, paymentIntentId, emails, cancellationToken);
                return;

            // Cancelled, failed, refunded or disputed: money arrived for a purchase that can't take it
            default:
                logger.LogWarning("A payment arrived for court purchase {PurchaseId} in status {Status}.", purchase.Id, purchase.Status);
                emails.Add(() => notifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.UnexpectedPayment, cancellationToken));
                return;
        }
    }

    private async Task SettleLatePayment(CourtPurchase purchase, bool paid, string? paymentIntentId, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        // Bookings and purchases of the club one after the other: the slot and the credit are checked against everything saved before
        await context.LockCourts(purchase.ClubId, cancellationToken);

        // An expired purchase gave its credit back, and the buyer may have spent it since. Without a credit the purchase is settled
        // without one and the treasurers decide (the buyer paid only the rest online).
        var creditLost = false;
        if (purchase is { Status: CourtPurchaseStatus.Expired, CreditUsed: true })
        {
            var balance = purchase.MemberId is { } memberId ? await context.GetCreditBalance(purchase.ClubId, memberId, cancellationToken) : 0;
            if (balance < 1)
            {
                purchase.DropCredit();
                creditLost = true;
            }
        }

        Book(purchase, paid, paymentIntentId);
        if (await TryBookSlotAgain(purchase, cancellationToken))
        {
            emails.Add(() => notifier.Confirmed(purchase, cancellationToken));
        }
        else
        {
            logger.LogWarning("Court purchase {PurchaseId} was paid without a hold on its slot and the slot is taken.", purchase.Id);
            emails.Add(() => notifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.PaidWithoutSlot, cancellationToken));
        }

        if (creditLost)
            emails.Add(() => notifier.NeedsAttention(purchase, CourtPurchaseAttentionReason.CreditNoLongerAvailable, cancellationToken));
    }

    public async Task ReleaseHold(CourtPurchase purchase, CancellationToken cancellationToken)
    {
        purchase.Expire();
        await RemoveBooking(purchase, onlyUpcoming: false, cancellationToken);
    }

    public async Task RemoveBooking(CourtPurchase purchase, bool onlyUpcoming, CancellationToken cancellationToken)
    {
        if (purchase.BookingId is not { } bookingId)
            return;

        // Also runs in background jobs without a tenant: the club of the purchase is checked explicitly
        var booking = await context.Bookings.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == bookingId && b.ClubId == purchase.ClubId, cancellationToken);
        if (booking is not null)
        {
            if (onlyUpcoming && booking.Interval.From <= DateTimeOffset.UtcNow)
                return;

            context.Bookings.Remove(booking);
        }

        purchase.DetachBooking();
    }

    public async Task<SessionCheck> CheckSession(CourtPurchase purchase, List<Func<Task>> emails, CancellationToken cancellationToken)
    {
        if (!gateway.IsConfigured || purchase.StripeCheckoutSessionId is not { } sessionId)
            return SessionCheck.Unknown;

        var accountId = await context.ClubPaymentAccounts.IgnoreQueryFilters()
            .Where(a => a.ClubId == purchase.ClubId)
            .Select(a => a.StripeAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (accountId is null)
            return SessionCheck.Unknown;

        StripeCheckoutSession? session;
        try
        {
            session = await gateway.GetCheckoutSession(accountId, sessionId, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "The payment page of court purchase {PurchaseId} could not be checked with Stripe.", purchase.Id);
            return SessionCheck.Unknown;
        }

        // A session that does not exist (any more) or belongs to something else proves nothing
        if (session is null || !session.IsForCourtPurchase(purchase.Id))
            return SessionCheck.Unknown;

        if (session.IsComplete)
        {
            await SettleCompletedSession(purchase, session, emails, cancellationToken);
            return SessionCheck.Settled;
        }

        return session.Status == "expired" ? SessionCheck.Expired : SessionCheck.Open;
    }

    private static void Book(CourtPurchase purchase, bool paid, string? paymentIntentId)
    {
        if (paid)
            purchase.MarkPaid(DateTimeOffset.UtcNow, paymentIntentId);
        else
            purchase.MarkProcessing(paymentIntentId);
    }

    /// <summary>The slot of an expired purchase is booked again when nothing else is on the court then and it has not started.</summary>
    private async Task<bool> TryBookSlotAgain(CourtPurchase purchase, CancellationToken cancellationToken)
    {
        if (purchase.MemberId is not { } memberId || purchase.Interval.From <= DateTimeOffset.UtcNow)
            return false;

        var settings = await context.CourtSaleSettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ClubId == purchase.ClubId, cancellationToken);
        var court = await context.Courts.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == purchase.CourtId && c.ClubId == purchase.ClubId, cancellationToken);
        if (settings?.PlayModeId is not { } playModeId || court is null || court.IsInactive(purchase.Interval))
            return false;

        var interval = purchase.Interval;
        var isTaken = await context.Bookings.IgnoreQueryFilters()
            .Where(b => b.ClubId == purchase.ClubId && b.CourtId == purchase.CourtId && b.Interval.Intersects(interval))
            .ExpandProjectables()
            .AnyAsync(cancellationToken);

        if (isTaken || (await context.GetCourtBlockingTitles(purchase.CourtId, interval, cancellationToken)).Count > 0)
            return false;

        // A copy of the interval: EF can't share an owned value between two entities
        var booking = new Booking(purchase.ClubId, purchase.CourtId, playModeId, new DateTimeOffsetInterval(interval.From, interval.To), settings.TimeZoneId, [memberId]);
        context.Bookings.Add(booking);
        purchase.AttachBooking(booking.Id);
        return true;
    }
}
