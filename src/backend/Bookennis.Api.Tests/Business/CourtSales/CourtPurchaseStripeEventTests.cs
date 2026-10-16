using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>The Stripe events of court purchases (<see cref="ProcessStripeEvent"/>), told apart from membership orders by the session reference.</summary>
public class CourtPurchaseStripeEventTests(TestFixture fixture) : CourtSaleTestBase(fixture)
{
    [Fact]
    public async Task CheckoutCompleted_Paid_MarksPaidAndConfirms()
    {
        var purchase = await SeedHold();
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        var paid = await Purchase(purchase.Id);
        paid.Status.Should().Be(CourtPurchaseStatus.Paid);
        paid.PaymentMethod.Should().Be(CourtPurchasePaymentMethod.Online);
        paid.StripePaymentIntentId.Should().Be("pi_court");
        (await BookingExists(purchase.BookingId)).Should().BeTrue();
        await Notifier.Received(1).Confirmed(Arg.Is<CourtPurchase>(p => p.Id == purchase.Id), Arg.Any<CancellationToken>());

        // Stripe sends it again
        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");
        await Notifier.Received(1).Confirmed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SepaDebit_KeepsTheBooking_AndAFailedDebitRemovesItAndTellsBuyerAndTreasurers()
    {
        var purchase = await SeedHold();
        StripeHasCourtSession(purchase.Id, "complete", "unpaid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Processing);
        (await BookingExists(purchase.BookingId)).Should().BeTrue();

        await Process(ProcessStripeEvent.CheckoutAsyncPaymentFailed, "cs_test");

        var failed = await Purchase(purchase.Id);
        failed.Status.Should().Be(CourtPurchaseStatus.Failed);
        failed.BookingId.Should().BeNull();
        (await BookingExists(purchase.BookingId)).Should().BeFalse();
        await Notifier.Received(1).PaymentFailed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>());
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.PaymentFailed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SepaDebit_Succeeds_MarksPaid()
    {
        var purchase = await SeedHold();
        StripeHasCourtSession(purchase.Id, "complete", "unpaid");
        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        await Process(ProcessStripeEvent.CheckoutAsyncPaymentSucceeded, "cs_test");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Paid);
        await Notifier.Received(1).Confirmed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutExpired_ReleasesTheHold_OnlyForTheCurrentPaymentPage()
    {
        var purchase = await SeedHold();
        StripeHasCourtSession(purchase.Id, "expired", "unpaid", sessionId: "cs_old");

        await Process(ProcessStripeEvent.CheckoutExpired, "cs_old");
        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment, "cs_old is not the page the purchase waits for");

        StripeHasCourtSession(purchase.Id, "expired", "unpaid");
        await Process(ProcessStripeEvent.CheckoutExpired, "cs_test");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Expired);
        (await BookingExists(purchase.BookingId)).Should().BeFalse();
    }

    [Fact]
    public async Task PaymentAfterTheHoldExpired_SlotStillFree_IsBookedAgain()
    {
        var purchase = await SeedHold();
        await QueryAsync(async ctx =>
        {
            var p = await ctx.CourtPurchases.SingleAsync();
            p.Expire();
            ctx.Bookings.Remove(await ctx.Bookings.SingleAsync(b => b.Id == p.BookingId));
            p.DetachBooking();
            await ctx.SaveChangesAsync();
            return 0;
        });
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        var paid = await Purchase(purchase.Id);
        paid.Status.Should().Be(CourtPurchaseStatus.Paid);
        paid.BookingId.Should().NotBeNull();
        var booking = await QueryAsync(ctx => ctx.Bookings.Include(b => b.Players).SingleAsync(b => b.Id == paid.BookingId));
        booking.Interval.Should().Be(purchase.Interval);
        booking.Players.Select(p => p.MemberId).Should().Equal(purchase.MemberId!.Value);
        await Notifier.Received(1).Confirmed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentAfterTheHoldExpired_SlotTaken_IsPaidWithoutBookingAndTheTreasurersAreTold()
    {
        var purchase = await SeedHold();
        await QueryAsync(async ctx =>
        {
            var p = await ctx.CourtPurchases.SingleAsync();
            p.Expire();
            ctx.Bookings.Remove(await ctx.Bookings.SingleAsync(b => b.Id == p.BookingId));
            p.DetachBooking();
            await ctx.SaveChangesAsync();
            return await TestUtils.MemberSeed.AddBooking(ctx, purchase.Interval.From, TestDataSeed.Member2Id);
        });
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        var paid = await Purchase(purchase.Id);
        paid.Status.Should().Be(CourtPurchaseStatus.Paid);
        paid.BookingId.Should().BeNull();
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.PaidWithoutSlot, Arg.Any<CancellationToken>());
        await Notifier.DidNotReceiveWithAnyArgs().Confirmed(default!, default);
    }

    [Fact]
    public async Task PaymentForACancelledPurchase_IsReportedToTheTreasurers()
    {
        var purchase = await SeedHold();
        await QueryAsync(async ctx =>
        {
            (await ctx.CourtPurchases.SingleAsync()).Cancel(null, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
            return 0;
        });
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Cancelled);
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.UnexpectedPayment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullRefund_MarksRefundedRemovesTheUpcomingBookingAndTellsTheTreasurers()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), paymentIntentId: "pi_refund"));
        Gateway.GetCharge(AccountId, "ch_1", Arg.Any<CancellationToken>()).Returns(new StripeChargeInfo("ch_1", "pi_refund", IsFullyRefunded: true));

        await Process(ProcessStripeEvent.ChargeRefunded, "ch_1");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Refunded);
        (await BookingExists(purchase.BookingId)).Should().BeFalse();
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.Refunded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispute_MarksDisputedAndTellsTheTreasurers()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), paymentIntentId: "pi_dispute"));
        Gateway.GetDispute(AccountId, "dp_1", Arg.Any<CancellationToken>()).Returns(new StripeDisputeInfo("dp_1", "pi_dispute"));

        await Process(ProcessStripeEvent.DisputeCreated, "dp_1");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Disputed);
        (await BookingExists(purchase.BookingId)).Should().BeTrue("a dispute does not take the slot");
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.Disputed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefundOfACompensatedPurchase_TakesTheCreditsBackWithoutMail()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await Compensated(memberId, "pi_comp_refund", minutes: 120);
        Gateway.GetCharge(AccountId, "ch_comp", Arg.Any<CancellationToken>()).Returns(new StripeChargeInfo("ch_comp", "pi_comp_refund", IsFullyRefunded: true));

        await Process(ProcessStripeEvent.ChargeRefunded, "ch_comp");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Refunded);
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, default))).Should().Be(0, "the money went back, so the 2 credits go too");
        await Notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    [Fact]
    public async Task DisputeOfACompensatedPurchase_WhoseCreditsWereUsed_TellsTheTreasurers()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await Compensated(memberId, "pi_comp_dispute", minutes: 120);
        // One of the two credits was spent on another slot
        await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(localHour: 15), creditUsed: true));
        Gateway.GetDispute(AccountId, "dp_comp", Arg.Any<CancellationToken>()).Returns(new StripeDisputeInfo("dp_comp", "pi_comp_dispute"));

        await Process(ProcessStripeEvent.DisputeCreated, "dp_comp");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Disputed);
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, default))).Should().Be(0, "the one credit left was taken back");
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.CompensationNotTakenBack, Arg.Any<CancellationToken>());
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.Disputed, Arg.Any<CancellationToken>());
    }

    /// <summary>A purchase paid online whose booking the club removed: the buyer got one credit per started hour.</summary>
    private async Task<CourtPurchase> Compensated(int memberId, string paymentIntentId, int minutes)
    {
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(minutes), paymentIntentId: paymentIntentId));
        await SendAsync(new NotifyBoughtBookingsRemoved([new BoughtBookingRemoval(purchase.Id, "CourtPurchase_Compensated_CourtInactive", null)]));
        Notifier.ClearReceivedCalls();
        return await Purchase(purchase.Id);
    }

    [Fact]
    public async Task PaymentForAHoldWhoseBookingTheClubRemoved_IsBookedAgainWhenFree_OtherwiseReported()
    {
        var purchase = await SeedHold();
        await QueryAsync(async ctx =>
        {
            // A court blocking removed the held booking: the purchase still waits for its payment, without booking
            ctx.Bookings.Remove(await ctx.Bookings.SingleAsync(b => b.Id == purchase.BookingId));
            await ctx.SaveChangesAsync();
            return 0;
        });
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        var paid = await Purchase(purchase.Id);
        paid.Status.Should().Be(CourtPurchaseStatus.Paid);
        paid.BookingId.Should().NotBeNull("the slot was free again");
        (await BookingExists(paid.BookingId)).Should().BeTrue();
        await Notifier.Received(1).Confirmed(Arg.Any<CourtPurchase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentForAHoldWhoseBookingTheClubRemoved_SlotTaken_IsReported()
    {
        var purchase = await SeedHold();
        await QueryAsync(async ctx =>
        {
            ctx.Bookings.Remove(await ctx.Bookings.SingleAsync(b => b.Id == purchase.BookingId));
            await ctx.SaveChangesAsync();
            return await TestUtils.MemberSeed.AddBooking(ctx, purchase.Interval.From, TestDataSeed.Member2Id);
        });
        StripeHasCourtSession(purchase.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        (await Purchase(purchase.Id)).BookingId.Should().BeNull();
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.PaidWithoutSlot, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LatePaymentOfAnExpiredCreditPurchase_CreditSpentMeanwhile_IsPaidWithoutCreditAndReported()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer(credits: 1, maxUpcomingBookings: 5);
        var expired = await QueryAsync(async ctx =>
        {
            // 2 hours: the credit paid the first hour, the rest was to be paid online; then the hold expired
            var purchase = new CourtPurchase(TestDataSeed.ClubId, memberId, TestDataSeed.Court1Id, null, CourtSaleSeed.Tomorrow(120), new CourtQuote(4000, "eur", []), true, 2000);
            purchase.StartCheckout("cs_test", DateTimeOffset.UtcNow.AddMinutes(-40));
            purchase.Expire();
            ctx.Add(purchase);
            await ctx.SaveChangesAsync();

            // The credit came back and was spent on another slot
            await CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(60, 15), creditUsed: true);
            return purchase;
        });
        StripeHasCourtSession(expired.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        var paid = await Purchase(expired.Id);
        paid.Status.Should().Be(CourtPurchaseStatus.Paid);
        paid.CreditUsed.Should().BeFalse();
        (await QueryAsync(ctx => ctx.GetCreditBalance(TestDataSeed.ClubId, memberId, CancellationToken.None))).Should().Be(0);
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.CreditNoLongerAvailable, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LatePaymentOfAnExpiredCreditPurchase_CreditStillThere_KeepsIt()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer(credits: 1);
        var expired = await QueryAsync(async ctx =>
        {
            var purchase = new CourtPurchase(TestDataSeed.ClubId, memberId, TestDataSeed.Court1Id, null, CourtSaleSeed.Tomorrow(120), new CourtQuote(4000, "eur", []), true, 2000);
            purchase.StartCheckout("cs_test", DateTimeOffset.UtcNow.AddMinutes(-40));
            purchase.Expire();
            ctx.Add(purchase);
            await ctx.SaveChangesAsync();
            return purchase;
        });
        StripeHasCourtSession(expired.Id, "complete", "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test");

        (await Purchase(expired.Id)).CreditUsed.Should().BeTrue();
        await Notifier.DidNotReceive().NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.CreditNoLongerAvailable, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispute_OfACancelledPurchase_IsReportedToo()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        var purchase = await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.Cancelled, paymentIntentId: "pi_cancelled", withBooking: false));
        Gateway.GetDispute(AccountId, "dp_2", Arg.Any<CancellationToken>()).Returns(new StripeDisputeInfo("dp_2", "pi_cancelled"));

        await Process(ProcessStripeEvent.DisputeCreated, "dp_2");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.Disputed);
        await Notifier.Received(1).NeedsAttention(Arg.Any<CourtPurchase>(), CourtPurchaseAttentionReason.Disputed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EventOfAnotherClubsAccount_DoesNotTouchThePurchase()
    {
        var purchase = await SeedHold();
        await SeedAccount(accountId: "acct_other", clubId: await QueryAsync(async ctx =>
        {
            var club = new Domain.Clubs.Club("Other", new Global.Intervals.TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)), new Global.Intervals.TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)), []);
            ctx.Add(club);
            await ctx.SaveChangesAsync();
            return club.Id;
        }));
        StripeHasCourtSession(purchase.Id, "complete", "paid", accountId: "acct_other");

        await Process(ProcessStripeEvent.CheckoutCompleted, "cs_test", accountId: "acct_other");

        (await Purchase(purchase.Id)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);
    }

    [Fact]
    public async Task MembershipSessionsKeepTheirPlainReference()
    {
        StripeCheckoutReference.TryParse("42", out var kind, out var id).Should().BeTrue();
        kind.Should().Be(StripeCheckoutKind.MembershipOrder);
        id.Should().Be(42);

        StripeCheckoutReference.TryParse(StripeCheckoutReference.For(StripeCheckoutKind.CourtPurchase, 7), out kind, out id).Should().BeTrue();
        kind.Should().Be(StripeCheckoutKind.CourtPurchase);
        id.Should().Be(7);

        StripeCheckoutReference.TryParse("court_purchase:abc", out _, out _).Should().BeFalse();
        StripeCheckoutReference.TryParse(null, out _, out _).Should().BeFalse();
        await Task.CompletedTask;
    }

    private async Task<CourtPurchase> SeedHold()
    {
        await SeedAccount();
        var (_, memberId) = await SeedBuyer();
        return await QueryAsync(ctx => CourtSaleSeed.AddPurchase(ctx, memberId, CourtSaleSeed.Tomorrow(), CourtPurchaseStatus.AwaitingPayment));
    }

    private async Task Process(string type, string objectId, string accountId = AccountId)
    {
        var id = await QueryAsync(async ctx =>
        {
            var stripeEvent = new StripeWebhookEvent($"evt_{Guid.NewGuid():N}", type, accountId, objectId, DateTimeOffset.UtcNow);
            ctx.Add(stripeEvent);
            await ctx.SaveChangesAsync();
            return stripeEvent.Id;
        });

        await SendAsync(new ProcessStripeEvent(id));
    }
}
