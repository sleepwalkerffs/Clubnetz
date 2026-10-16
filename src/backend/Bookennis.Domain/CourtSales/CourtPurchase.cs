using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.CourtSales;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus</c>.</summary>
public enum CourtPurchaseStatus
{
    /// <summary>The slot is held by the booking while the buyer pays online.</summary>
    AwaitingPayment = 0,

    /// <summary>A SEPA direct debit was started and has not settled yet. The booking stays.</summary>
    Processing = 1,
    Paid = 2,

    /// <summary>The buyer did not pay in time: the hold was released and the booking deleted.</summary>
    Expired = 3,

    /// <summary>The debit did not go through.</summary>
    Failed = 4,

    /// <summary>Cancelled by the treasurer. Money paid online is given back in the Stripe dashboard.</summary>
    Cancelled = 5,
    Refunded = 6,
    Disputed = 7,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.CourtSales.CourtPurchasePaymentMethod</c>.</summary>
public enum CourtPurchasePaymentMethod
{
    /// <summary>Paid with Stripe Checkout (also the rest of a longer booking that was partly paid with a credit).</summary>
    Online = 0,

    /// <summary>One booking credit paid for all of it.</summary>
    Credit = 1,

    /// <summary>The price rule was free.</summary>
    Free = 2,
}

/// <summary>
/// A member who is not enrolled in the active season bought a slot on a court. The <see cref="BookingId">booking</see> is created
/// when the purchase starts, so the slot is held while the buyer pays. The purchase stays as the club's record of the payment
/// when the booking or the buyer is gone.
/// </summary>
[Guid("C2E8A4F1-6B3D-4A9C-8E57-1D0F3B6A9C25")]
public class CourtPurchase : TenantDomainEntity, IAggregateRoot
{
    public const int MaxStripeIdLength = 255;

    /// <summary>The purchases that hold the slot and count toward the limit of upcoming bookings of a buyer.</summary>
    public static readonly CourtPurchaseStatus[] ActiveStatuses = [CourtPurchaseStatus.AwaitingPayment, CourtPurchaseStatus.Processing, CourtPurchaseStatus.Paid];

    /// <summary>
    /// A purchase in one of these states keeps the credit it used. A purchase that expires, fails or is cancelled gives it back,
    /// because the balance is calculated from the purchases.
    /// </summary>
    public static readonly CourtPurchaseStatus[] CreditHoldingStatuses = ActiveStatuses;

#pragma warning disable CS8618
    private CourtPurchase() { }
#pragma warning restore CS8618

    public CourtPurchase(int clubId, int memberId, int courtId, int? bookingId, DateTimeOffsetInterval interval, CourtQuote quote, bool creditUsed, int amountDueCents)
    {
        if (amountDueCents < 0 || amountDueCents > quote.PriceCents)
            throw new ArgumentOutOfRangeException(nameof(amountDueCents), amountDueCents, "The amount due must be between 0 and the price.");

        ClubId = clubId;
        MemberId = memberId;
        CourtId = courtId;
        BookingId = bookingId;

        // A copy: EF stores the interval as an owned value of each entity and can't share one instance with the booking
        Interval = new DateTimeOffsetInterval(interval.From, interval.To);
        PriceCents = quote.PriceCents;
        Currency = quote.Currency;
        CreditUsed = creditUsed;
        AmountDueCents = amountDueCents;
        Status = CourtPurchaseStatus.AwaitingPayment;
    }

    /// <summary>The buyer; <c>null</c> once the member left the club.</summary>
    public int? MemberId { get; private set; }

    /// <summary>The booking that holds the slot; <c>null</c> once it was deleted (hold released, cancelled, refunded).</summary>
    public int? BookingId { get; private set; }

    public int CourtId { get; private set; }
    public DateTimeOffsetInterval Interval { get; private set; }

    /// <summary>The full price of the slot.</summary>
    public int PriceCents { get; private set; }

    /// <summary>A booking credit paid for the slot (or its first hour).</summary>
    public bool CreditUsed { get; private set; }

    /// <summary>What is paid online; 0 when a credit or a free rule paid for everything.</summary>
    public int AmountDueCents { get; private set; }

    public string Currency { get; private set; }
    public CourtPurchaseStatus Status { get; private set; }
    public CourtPurchasePaymentMethod? PaymentMethod { get; private set; }
    public string? StripeCheckoutSessionId { get; private set; }
    public string? StripePaymentIntentId { get; private set; }

    /// <summary>When the slot is given free again if nothing was paid.</summary>
    public DateTimeOffset? HoldExpiresAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public int? CancelledByMemberId { get; private set; }

    public bool IsActive => ActiveStatuses.Contains(Status);

    /// <summary>Money was or is being paid online for the purchase: the treasurer gives it back in the Stripe dashboard.</summary>
    public bool HasOnlinePayment => PaymentMethod == CourtPurchasePaymentMethod.Online && Status is CourtPurchaseStatus.Processing or CourtPurchaseStatus.Paid or CourtPurchaseStatus.Disputed or CourtPurchaseStatus.Cancelled or CourtPurchaseStatus.Refunded;

    /// <summary>Nothing is paid online: a credit or a free rule paid for the slot. The purchase is paid right away.</summary>
    public void CompleteWithoutPayment(DateTimeOffset paidAt)
    {
        Require(Status == CourtPurchaseStatus.AwaitingPayment && AmountDueCents == 0);
        Status = CourtPurchaseStatus.Paid;
        PaymentMethod = CreditUsed ? CourtPurchasePaymentMethod.Credit : CourtPurchasePaymentMethod.Free;
        PaidAt = paidAt;
    }

    /// <summary>The payment page was created; the slot is held until <paramref name="holdExpiresAt"/>.</summary>
    public void StartCheckout(string checkoutSessionId, DateTimeOffset holdExpiresAt)
    {
        Require(Status == CourtPurchaseStatus.AwaitingPayment && AmountDueCents > 0);
        StripeCheckoutSessionId = checkoutSessionId;
        HoldExpiresAt = holdExpiresAt;
    }

    /// <summary>The buyer confirmed the payment, but the money is still on its way (SEPA direct debit). No-op when it already arrived.</summary>
    public void MarkProcessing(string? paymentIntentId)
    {
        if (Status is CourtPurchaseStatus.Processing or CourtPurchaseStatus.Paid)
        {
            StripePaymentIntentId ??= paymentIntentId;
            return;
        }

        Require(Status is CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Expired);
        Status = CourtPurchaseStatus.Processing;
        PaymentMethod = CourtPurchasePaymentMethod.Online;
        StripePaymentIntentId = paymentIntentId;
        HoldExpiresAt = null;
    }

    /// <summary>
    /// The slot was paid online. Also for a purchase whose hold already expired: the money arrived, the purchase is paid
    /// (the caller decides about the slot). No-op when it is already paid, because Stripe may report a payment twice.
    /// </summary>
    public void MarkPaid(DateTimeOffset paidAt, string? paymentIntentId)
    {
        if (Status == CourtPurchaseStatus.Paid)
        {
            StripePaymentIntentId ??= paymentIntentId;
            return;
        }

        Require(Status is CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Expired);
        Status = CourtPurchaseStatus.Paid;
        PaymentMethod = CourtPurchasePaymentMethod.Online;
        PaidAt = paidAt;
        StripePaymentIntentId = paymentIntentId ?? StripePaymentIntentId;
        HoldExpiresAt = null;
    }

    /// <summary>The debit did not go through. No-op when that is already known.</summary>
    public void MarkFailed()
    {
        if (Status == CourtPurchaseStatus.Failed)
            return;

        Require(Status is CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Processing);
        Status = CourtPurchaseStatus.Failed;
        HoldExpiresAt = null;
    }

    /// <summary>Nothing was paid in time: the hold is released. No-op when that already happened.</summary>
    public void Expire()
    {
        if (Status == CourtPurchaseStatus.Expired)
            return;

        Require(Status == CourtPurchaseStatus.AwaitingPayment);
        Status = CourtPurchaseStatus.Expired;
        HoldExpiresAt = null;
    }

    /// <summary>The treasurer cancels the purchase. Money paid online is refunded in the Stripe dashboard, a credit comes back by itself.</summary>
    public void Cancel(int? cancelledByMemberId, DateTimeOffset cancelledAt)
    {
        Require(Status is CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Paid);
        Status = CourtPurchaseStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancelledByMemberId = cancelledByMemberId;
        HoldExpiresAt = null;
    }

    /// <summary>The whole amount was refunded in Stripe. No-op when that is already known.</summary>
    public void MarkRefunded()
    {
        if (Status == CourtPurchaseStatus.Refunded)
            return;

        Require(Status is CourtPurchaseStatus.Paid or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Disputed or CourtPurchaseStatus.Cancelled);
        Status = CourtPurchaseStatus.Refunded;
    }

    /// <summary>The buyer disputes the payment at their bank. No-op when that is already known.</summary>
    public void MarkDisputed()
    {
        if (Status == CourtPurchaseStatus.Disputed)
            return;

        // Also a cancelled purchase: the money was not refunded yet and the bank takes it back
        Require(Status is CourtPurchaseStatus.Paid or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Cancelled);
        Status = CourtPurchaseStatus.Disputed;
    }

    /// <summary>
    /// The money of an expired purchase arrived, but its credit was spent on something else in the meantime: the purchase is
    /// settled without a credit (only <see cref="AmountDueCents"/> was paid) and the treasurers decide.
    /// </summary>
    public void DropCredit()
    {
        Require(Status is CourtPurchaseStatus.Expired or CourtPurchaseStatus.AwaitingPayment);
        CreditUsed = false;
    }

    /// <summary>The booking was deleted (or is about to be).</summary>
    public void DetachBooking() => BookingId = null;

    /// <summary>A new booking holds the slot again, e.g. when the payment of an expired hold arrived and the slot was still free.</summary>
    public void AttachBooking(int bookingId) => BookingId = bookingId;

    private void Require(bool allowed)
    {
        if (!allowed)
            throw new PreconditionException(CourtSaleErrorCode.CourtPurchaseInvalidStatus, [Status.ToString()], $"The purchase is {Status}.");
    }
}
