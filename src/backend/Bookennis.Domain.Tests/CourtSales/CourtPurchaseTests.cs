using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.CourtSales;

public class CourtPurchaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffsetInterval Slot = new(Now.AddDays(1), Now.AddDays(1).AddHours(1));

    [Fact]
    public void CreditOrFreeRule_IsPaidRightAway()
    {
        var credit = Purchase(price: 2800, creditUsed: true, amountDue: 0);
        var free = Purchase(price: 0, creditUsed: false, amountDue: 0);

        credit.CompleteWithoutPayment(Now);
        free.CompleteWithoutPayment(Now);

        credit.Status.Should().Be(CourtPurchaseStatus.Paid);
        credit.PaymentMethod.Should().Be(CourtPurchasePaymentMethod.Credit);
        free.PaymentMethod.Should().Be(CourtPurchasePaymentMethod.Free);
        free.PaidAt.Should().Be(Now);
    }

    [Fact]
    public void CompleteWithoutPayment_WithAnAmountDue_Throws()
    {
        var act = () => Purchase().CompleteWithoutPayment(Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPurchaseInvalidStatus));
    }

    [Fact]
    public void AmountDue_MustBeBetweenZeroAndThePrice()
    {
        var act = () => Purchase(price: 1000, amountDue: 1001);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OnlinePayment_HoldsUntilPaid()
    {
        var purchase = Purchase();
        purchase.StartCheckout("cs_1", Now.AddMinutes(31));

        purchase.HoldExpiresAt.Should().Be(Now.AddMinutes(31));
        purchase.MarkPaid(Now, "pi_1");

        purchase.Status.Should().Be(CourtPurchaseStatus.Paid);
        purchase.PaymentMethod.Should().Be(CourtPurchasePaymentMethod.Online);
        purchase.StripePaymentIntentId.Should().Be("pi_1");
        purchase.HoldExpiresAt.Should().BeNull();
        purchase.HasOnlinePayment.Should().BeTrue();
    }

    [Fact]
    public void StripeRepeatsItself_TheTransitionsAreIdempotent()
    {
        var purchase = Purchase();
        purchase.StartCheckout("cs_1", Now.AddMinutes(31));

        purchase.MarkProcessing("pi_1");
        purchase.MarkProcessing("pi_1");
        purchase.Status.Should().Be(CourtPurchaseStatus.Processing);

        purchase.MarkPaid(Now, "pi_1");
        purchase.MarkPaid(Now.AddHours(1), "pi_1");
        purchase.MarkProcessing("pi_1");
        purchase.Status.Should().Be(CourtPurchaseStatus.Paid);
        purchase.PaidAt.Should().Be(Now);

        purchase.MarkDisputed();
        purchase.MarkDisputed();
        purchase.MarkRefunded();
        purchase.MarkRefunded();
        purchase.Status.Should().Be(CourtPurchaseStatus.Refunded);
    }

    [Fact]
    public void Expire_OnlyWhileAwaitingPayment_AndRepeatable()
    {
        var purchase = Purchase();
        purchase.Expire();
        purchase.Expire();
        purchase.Status.Should().Be(CourtPurchaseStatus.Expired);

        var paid = Purchase();
        paid.MarkPaid(Now, "pi");
        var act = () => paid.Expire();
        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPurchaseInvalidStatus));
    }

    [Fact]
    public void PaymentAfterTheHoldExpired_StillMarksThePurchasePaid()
    {
        var purchase = Purchase();
        purchase.Expire();

        purchase.MarkPaid(Now, "pi_late");

        purchase.Status.Should().Be(CourtPurchaseStatus.Paid);
    }

    [Fact]
    public void Failed_OnlyFromAwaitingPaymentOrProcessing()
    {
        var processing = Purchase();
        processing.MarkProcessing("pi");
        processing.MarkFailed();
        processing.MarkFailed();
        processing.Status.Should().Be(CourtPurchaseStatus.Failed);

        var paid = Purchase();
        paid.MarkPaid(Now, "pi");
        var act = () => paid.MarkFailed();
        act.Should().Throw<PreconditionException>();
    }

    [Fact]
    public void Cancel_ByTheTreasurer_RemembersWhoAndWhen()
    {
        var purchase = Purchase();
        purchase.MarkPaid(Now, "pi");

        purchase.Cancel(42, Now.AddHours(1));

        purchase.Status.Should().Be(CourtPurchaseStatus.Cancelled);
        purchase.CancelledByMemberId.Should().Be(42);
        purchase.CancelledAt.Should().Be(Now.AddHours(1));
        purchase.HasOnlinePayment.Should().BeTrue("the money has to be refunded in the Stripe dashboard");

        var again = () => purchase.Cancel(42, Now);
        again.Should().Throw<PreconditionException>();
    }

    [Fact]
    public void Cancel_PurchasePaidWithACredit_HasNoOnlinePayment()
    {
        var purchase = Purchase(price: 2000, creditUsed: true, amountDue: 0);
        purchase.CompleteWithoutPayment(Now);

        purchase.Cancel(null, Now);

        purchase.HasOnlinePayment.Should().BeFalse();
    }

    [Fact]
    public void CreditHoldingStatuses_GiveTheCreditBackWhenThePurchaseEnds()
    {
        CourtPurchase.CreditHoldingStatuses.Should().BeEquivalentTo([CourtPurchaseStatus.AwaitingPayment, CourtPurchaseStatus.Processing, CourtPurchaseStatus.Paid]);
    }

    [Fact]
    public void DropCredit_OnlyForAPurchaseThatWaitsOrExpired()
    {
        var purchase = Purchase(price: 4000, creditUsed: true, amountDue: 2000);
        purchase.Expire();
        purchase.DropCredit();
        purchase.CreditUsed.Should().BeFalse();

        var paid = Purchase(price: 4000, creditUsed: true, amountDue: 2000);
        paid.MarkPaid(Now, "pi");
        var act = () => paid.DropCredit();
        act.Should().Throw<PreconditionException>();
    }

    [Fact]
    public void Dispute_OfACancelledPurchase_IsPossible()
    {
        var purchase = Purchase();
        purchase.MarkPaid(Now, "pi");
        purchase.Cancel(null, Now);

        purchase.MarkDisputed();

        purchase.Status.Should().Be(CourtPurchaseStatus.Disputed);
    }

    [Fact]
    public void Booking_CanBeDetachedAndAttachedAgain()
    {
        var purchase = Purchase();
        purchase.DetachBooking();
        purchase.BookingId.Should().BeNull();

        purchase.AttachBooking(9);
        purchase.BookingId.Should().Be(9);
    }

    // ---------- settings and credits ----------

    [Fact]
    public void Settings_DefaultsAreSwitchedOff()
    {
        var settings = new CourtSaleSettings(1);

        settings.IsEnabled.Should().BeFalse();
        settings.TimeZoneId.Should().Be("Europe/Vienna");
        settings.MaxBookingMinutes.Should().Be(120);
        settings.MaxDaysInAdvance.Should().Be(14);
        settings.MaxUpcomingBookings.Should().Be(1);
    }

    [Theory]
    [InlineData(true, null, "Europe/Vienna", 120, 14, 1, nameof(CourtSaleErrorCode.CourtSalePlayModeRequired))]
    [InlineData(false, 1, "Mars/Olympus", 120, 14, 1, nameof(CourtSaleErrorCode.CourtSaleInvalidTimeZone))]
    [InlineData(false, 1, "Europe/Vienna", 45, 14, 1, nameof(CourtSaleErrorCode.CourtSaleInvalidMaxBookingMinutes))]
    [InlineData(false, 1, "Europe/Vienna", 270, 14, 1, nameof(CourtSaleErrorCode.CourtSaleInvalidMaxBookingMinutes))]
    [InlineData(false, 1, "Europe/Vienna", 120, 91, 1, nameof(CourtSaleErrorCode.CourtSaleInvalidMaxDaysInAdvance))]
    [InlineData(false, 1, "Europe/Vienna", 120, 14, 0, nameof(CourtSaleErrorCode.CourtSaleInvalidMaxUpcomingBookings))]
    [InlineData(false, 1, "Europe/Vienna", 120, 14, 11, nameof(CourtSaleErrorCode.CourtSaleInvalidMaxUpcomingBookings))]
    public void Settings_InvalidValues_AreRejected(bool enabled, int? playModeId, string timeZone, int maxMinutes, int maxDays, int maxUpcoming, string errorCode)
    {
        var act = () => new CourtSaleSettings(1).Update(new CourtSaleSettings.SettingsData(enabled, playModeId, timeZone, maxMinutes, maxDays, maxUpcoming));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void Settings_RemovingThePlayMode_SwitchesTheSaleOff()
    {
        var settings = new CourtSaleSettings(1);
        settings.Update(new CourtSaleSettings.SettingsData(true, 5, "Europe/Berlin", 90, 7, 2));
        settings.IsEnabled.Should().BeTrue();
        settings.TimeZoneId.Should().Be("Europe/Berlin");

        settings.RemovePlayMode();

        settings.IsEnabled.Should().BeFalse();
        settings.PlayModeId.Should().BeNull();
    }

    [Fact]
    public void CreditGrant_OfZero_OrWithATooLongNote_IsRejected()
    {
        var zero = () => new BookingCreditGrant(1, 2, 0, BookingCreditReason.GrantedByClub, null, null);
        var note = () => new BookingCreditGrant(1, 2, 1, BookingCreditReason.GrantedByClub, new string('x', 501), null);

        zero.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.BookingCreditCountInvalid));
        note.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.BookingCreditNoteTooLong));
    }

    [Fact]
    public void CreditGrant_Negative_TakesCreditsBack()
    {
        var grant = new BookingCreditGrant(1, 2, -2, BookingCreditReason.GrantedByClub, "  correction  ", 3);

        grant.Count.Should().Be(-2);
        grant.Note.Should().Be("correction");
        grant.GrantedByMemberId.Should().Be(3);
    }

    [Theory]
    [InlineData(30, false, 1, 1)]
    [InlineData(60, false, 1, 1)]
    [InlineData(61, false, 2, 2)]
    [InlineData(90, true, 2, 1)]
    [InlineData(60, true, 1, 0)]
    [InlineData(180, false, 3, 3)]
    public void Compensate_OneCreditPerStartedHour_TheUsedCreditCountsTowardIt(int minutes, bool creditUsed, int credits, int granted)
    {
        var purchase = Purchase(minutes: minutes, creditUsed: creditUsed, amountDue: creditUsed && minutes <= 60 ? 0 : 1000);
        Pay(purchase);

        purchase.Compensate(Now);

        purchase.Status.Should().Be(CourtPurchaseStatus.Compensated);
        purchase.CompensationCredits.Should().Be(credits);
        purchase.GrantedCompensationCredits.Should().Be(granted);
        purchase.BookingId.Should().BeNull();
        purchase.CancelledAt.Should().Be(Now);
    }

    [Fact]
    public void Compensate_FreeSlot_GivesNoCredits()
    {
        var purchase = Purchase(price: 0, amountDue: 0);
        purchase.CompleteWithoutPayment(Now);

        purchase.Compensate(Now);

        purchase.CompensationCredits.Should().Be(0);
        purchase.GrantedCompensationCredits.Should().Be(0);
    }

    [Fact]
    public void Compensate_OnlyAPaidPurchase()
    {
        var processing = Purchase();
        processing.StartCheckout("cs_1", Now.AddMinutes(31));
        processing.MarkProcessing("pi_1");

        var act = () => processing.Compensate(Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPurchaseInvalidStatus));
    }

    [Fact]
    public void CompensatedPurchase_CanStillBeRefundedOrDisputed()
    {
        var refunded = Purchase();
        Pay(refunded);
        refunded.Compensate(Now);
        var disputed = Purchase();
        Pay(disputed);
        disputed.Compensate(Now);

        refunded.MarkRefunded();
        disputed.MarkDisputed();

        refunded.Status.Should().Be(CourtPurchaseStatus.Refunded);
        disputed.Status.Should().Be(CourtPurchaseStatus.Disputed);
        refunded.HasOnlinePayment.Should().BeTrue();
    }

    private static void Pay(CourtPurchase purchase)
    {
        if (purchase.AmountDueCents == 0)
        {
            purchase.CompleteWithoutPayment(Now);
            return;
        }

        purchase.StartCheckout("cs_1", Now.AddMinutes(31));
        purchase.MarkPaid(Now, "pi_1");
    }

    private static CourtPurchase Purchase(int price = 2800, bool creditUsed = false, int? amountDue = null, int minutes = 60)
        => new(1, 7, 1, 100, new DateTimeOffsetInterval(Slot.From, Slot.From.AddMinutes(minutes)), new CourtQuote(price, "eur", []), creditUsed, amountDue ?? price);
}
