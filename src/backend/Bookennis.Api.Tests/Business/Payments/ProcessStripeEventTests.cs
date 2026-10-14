using System.Drawing;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.Business.Memberships;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Payments;

public class ProcessStripeEventTests(TestFixture fixture) : StripeWebhookTestBase(fixture)
{
    [Fact]
    public async Task CheckoutCompleted_Paid_MarksTheOrderPaidEnrollsAndThanksThePayer()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        var order = await Order(orderId);
        order.Should().BeEquivalentTo(new { Status = MembershipOrderStatus.Paid, PaymentMethod = MembershipPaymentMethod.Online, StripePaymentIntentId = PaymentIntentId, MarkedPaidByMemberId = (int?)null }, o => o.ExcludingMissingMembers());
        order.PaidAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var enrollments = await QueryAsync(ctx => ctx.MemberSeasons.ToListAsync());
        enrollments.Select(e => e.MemberId).Should().BeEquivalentTo(family.Everybody);
        enrollments.Should().OnlyContain(e => e.SeasonId == family.SeasonId && e.MembershipOrderId == orderId);

        await Notifier.Received(1).PaymentReceived(Arg.Is<MembershipOrder>(o => o.Id == orderId), Arg.Any<CancellationToken>());
        await Notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    [Fact]
    public async Task CheckoutCompleted_SepaDebitStarted_EnrollsRightAwayAndTheLaterSuccessOnlyMarksPaid()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "unpaid");

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        var order = await Order(orderId);
        order.Status.Should().Be(MembershipOrderStatus.Processing);
        order.StripePaymentIntentId.Should().Be(PaymentIntentId);
        order.PaidAt.Should().BeNull();
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody, "the member does not wait days for the debit to settle");
        await Notifier.Received(1).PaymentReceived(Arg.Any<MembershipOrder>(), Arg.Any<CancellationToken>());

        // Days later the money arrives
        StripeHasSession(orderId, "paid");
        await Process(ProcessStripeEvent.CheckoutAsyncPaymentSucceeded, SessionId);

        order = await Order(orderId);
        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.PaidAt.Should().NotBeNull();
        (await EnrolledMemberIds()).Should().HaveCount(4);
        await Notifier.Received(1).PaymentReceived(Arg.Any<MembershipOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutAsyncPaymentFailed_MarksFailedTellsPayerAndTreasurersAndTakesTheMembersOut_PayingAgainEnrollsThemAgain()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "unpaid");
        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        await Process(ProcessStripeEvent.CheckoutAsyncPaymentFailed, SessionId);

        var order = await Order(orderId);
        order.Status.Should().Be(MembershipOrderStatus.Failed);
        order.IsPayable.Should().BeTrue();
        (await EnrolledMemberIds()).Should().BeEmpty("the debit that enrolled them failed");

        await Notifier.Received(1).PaymentFailed(Arg.Is<MembershipOrder>(o => o.Id == orderId), Arg.Any<CancellationToken>());
        await Notifier.Received(1).NeedsAttention(Arg.Is<MembershipOrder>(o => o.Id == orderId), MembershipAttentionReason.PaymentFailed, Arg.Any<CancellationToken>());

        // The member pays again, this time by card
        StripeHasSession(orderId, "paid");
        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Paid);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);
    }

    [Fact]
    public async Task CheckoutExpired_OrderGoesBackToWaitingForItsPayment()
    {
        var (_, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "unpaid");

        await Process(ProcessStripeEvent.CheckoutExpired, SessionId);

        var order = await Order(orderId);
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        order.StripeCheckoutSessionId.Should().BeNull();
        (await EnrolledMemberIds()).Should().BeEmpty();
    }

    [Fact]
    public async Task ChargeRefunded_FullRefund_MarksRefundedAndTellsTheTreasurers()
    {
        var (family, orderId) = await SeedPaidOrder();
        Gateway.GetCharge(AccountId, "ch_1", Arg.Any<CancellationToken>()).Returns(new StripeChargeInfo("ch_1", PaymentIntentId, IsFullyRefunded: true));

        await Process(ProcessStripeEvent.ChargeRefunded, "ch_1");

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Refunded);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);
        await Notifier.Received(1).NeedsAttention(Arg.Is<MembershipOrder>(o => o.Id == orderId), MembershipAttentionReason.Refunded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeRefunded_PartialRefund_ChangesNothing()
    {
        var (_, orderId) = await SeedPaidOrder();
        Gateway.GetCharge(AccountId, "ch_1", Arg.Any<CancellationToken>()).Returns(new StripeChargeInfo("ch_1", PaymentIntentId, IsFullyRefunded: false));

        await Process(ProcessStripeEvent.ChargeRefunded, "ch_1");

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Paid);
        await Notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    [Fact]
    public async Task ChargeRefunded_OrderTheTreasurerCancelled_BecomesRefundedWithoutAnEmail()
    {
        var (_, orderId) = await SeedPaidOrder(order => order.CancelByTreasurer());
        Gateway.GetCharge(AccountId, "ch_1", Arg.Any<CancellationToken>()).Returns(new StripeChargeInfo("ch_1", PaymentIntentId, IsFullyRefunded: true));

        await Process(ProcessStripeEvent.ChargeRefunded, "ch_1");

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Refunded);
        await Notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    [Fact]
    public async Task DisputeCreated_MarksDisputedAndTellsTheTreasurers()
    {
        var (family, orderId) = await SeedPaidOrder();
        Gateway.GetDispute(AccountId, "dp_1", Arg.Any<CancellationToken>()).Returns(new StripeDisputeInfo("dp_1", PaymentIntentId));

        await Process(ProcessStripeEvent.DisputeCreated, "dp_1");

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Disputed);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);
        await Notifier.Received(1).NeedsAttention(Arg.Is<MembershipOrder>(o => o.Id == orderId), MembershipAttentionReason.Disputed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AccountUpdated_TakesOverTheStatusOfTheAccount()
    {
        await SeedAccount(chargesEnabled: false);
        Gateway.GetAccount(AccountId, Arg.Any<CancellationToken>()).Returns(new StripeAccountInfo(AccountId, true, true, true));

        await Process(ProcessStripeEvent.AccountUpdated, AccountId);

        var account = await QueryAsync(ctx => ctx.ClubPaymentAccounts.SingleAsync());
        account.Should().BeEquivalentTo(new { ChargesEnabled = true, PayoutsEnabled = true, DetailsSubmitted = true }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task AccountDeauthorized_AccountNoLongerReachable_DisconnectsTheClub()
    {
        await SeedAccount();
        Gateway.GetAccount(AccountId, Arg.Any<CancellationToken>()).Returns((StripeAccountInfo?)null);

        await Process(ProcessStripeEvent.AccountDeauthorized, "ca_1");

        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task AccountDeauthorized_AccountStillReachable_StaysConnected()
    {
        await SeedAccount();
        Gateway.GetAccount(AccountId, Arg.Any<CancellationToken>()).Returns(new StripeAccountInfo(AccountId, true, true, true));

        await Process(ProcessStripeEvent.AccountDeauthorized, "ca_1");

        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.AnyAsync())).Should().BeTrue("the event alone is not trusted");
    }

    [Fact]
    public async Task CheckoutCompleted_OrderOfAnotherClub_ChangesNothing()
    {
        // Club B has its own Stripe account. Its session claims an order of the seeded club (club A).
        var (family, orderOfClubA) = await SeedOrderInCheckout();
        var clubB = await SeedOtherClubWithAccount("acct_club_b");
        Gateway.GetCheckoutSession("acct_club_b", SessionId, Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession(SessionId, null, "complete", "paid", orderOfClubA.ToString(), TestDataSeed.ClubId.ToString(), "pi_b"));

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId, accountId: "acct_club_b");

        var order = await Order(orderOfClubA);
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment, "which club an event is for is decided by the Stripe account it came from");
        order.StripePaymentIntentId.Should().BeNull();
        (await EnrolledMemberIds()).Should().BeEmpty();
        await Notifier.DidNotReceiveWithAnyArgs().PaymentReceived(default!, default);
        family.SeasonId.Should().BePositive();
        clubB.Should().NotBe(TestDataSeed.ClubId);
    }

    [Fact]
    public async Task ChargeRefunded_PaymentOfAnotherClub_ChangesNothing()
    {
        var (_, orderOfClubA) = await SeedPaidOrder();
        await SeedOtherClubWithAccount("acct_club_b");
        Gateway.GetCharge("acct_club_b", "ch_b", Arg.Any<CancellationToken>()).Returns(new StripeChargeInfo("ch_b", PaymentIntentId, IsFullyRefunded: true));

        await Process(ProcessStripeEvent.ChargeRefunded, "ch_b", accountId: "acct_club_b");

        (await Order(orderOfClubA)).Status.Should().Be(MembershipOrderStatus.Paid);
    }

    [Theory]
    [InlineData("999999")]
    [InlineData("not-a-number")]
    [InlineData(null)]
    public async Task CheckoutCompleted_UnknownOrder_IsProcessedWithoutError(string? clientReferenceId)
    {
        await SeedAccount();
        Gateway.GetCheckoutSession(AccountId, SessionId, Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession(SessionId, null, "complete", "paid", clientReferenceId, null, PaymentIntentId));

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId, eventId: "evt_unknown_order");

        (await QueryAsync(ctx => ctx.StripeWebhookEvents.SingleAsync(e => e.EventId == "evt_unknown_order"))).ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AnyEvent_AccountThatBelongsToNoClub_IsIgnored()
    {
        var (_, orderId) = await SeedOrderInCheckout();

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId, accountId: "acct_stranger", eventId: "evt_stranger");
        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId, accountId: null, eventId: "evt_platform");

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        await Gateway.DidNotReceiveWithAnyArgs().GetCheckoutSession(default!, default!, default);
        (await QueryAsync(ctx => ctx.StripeWebhookEvents.CountAsync(e => e.ProcessedAt != null))).Should().Be(2);
    }

    [Fact]
    public async Task CheckoutCompleted_OrderWasCancelledWhileThePaymentPageWasOpen_TellsTheTreasurers()
    {
        var (_, orderId) = await SeedOrderInCheckout(order => order.CancelByMember());
        StripeHasSession(orderId, "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Cancelled);
        (await EnrolledMemberIds()).Should().BeEmpty();
        await Notifier.Received(1).NeedsAttention(Arg.Is<MembershipOrder>(o => o.Id == orderId), MembershipAttentionReason.UnexpectedPayment, Arg.Any<CancellationToken>());
        await Notifier.DidNotReceiveWithAnyArgs().PaymentReceived(default!, default);
    }

    [Fact]
    public async Task CheckoutCompleted_OrderTheTreasurerAlreadyMarkedPaid_KeepsTheManualPaymentAndTellsTheTreasurers()
    {
        var (_, orderId) = await SeedOrderInCheckout(order => order.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow.AddHours(-1), TestDataSeed.Member2Id));
        StripeHasSession(orderId, "paid");

        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        var order = await Order(orderId);
        order.PaymentMethod.Should().Be(MembershipPaymentMethod.Cash);
        order.StripePaymentIntentId.Should().Be(PaymentIntentId, "a later refund in Stripe still finds the order");
        await Notifier.DidNotReceiveWithAnyArgs().PaymentReceived(default!, default);
        await Notifier.Received(1).NeedsAttention(Arg.Is<MembershipOrder>(o => o.Id == orderId), MembershipAttentionReason.UnexpectedPayment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessStripeEvent_EventThatWasProcessed_IsNotProcessedAgain()
    {
        var (_, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");
        var eventId = await QueryAsync(async ctx =>
        {
            var stripeEvent = new StripeWebhookEvent("evt_done", ProcessStripeEvent.CheckoutCompleted, AccountId, SessionId, DateTimeOffset.UtcNow);
            stripeEvent.MarkProcessed(DateTimeOffset.UtcNow);
            ctx.Add(stripeEvent);
            await ctx.SaveChangesAsync();
            return stripeEvent.Id;
        });

        await SendAsync(new ProcessStripeEvent(eventId));
        await SendAsync(new ProcessStripeEvent(987654));

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        await Gateway.DidNotReceiveWithAnyArgs().GetCheckoutSession(default!, default!, default);
    }

    private async Task<(SeededFamily Family, int OrderId)> SeedPaidOrder(Action<MembershipOrder>? change = null)
    {
        var (family, orderId) = await SeedOrderInCheckout(order =>
        {
            order.MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow.AddDays(-10), paymentIntentId: PaymentIntentId);
            change?.Invoke(order);
        });

        await QueryAsync(async ctx =>
        {
            foreach (var memberId in family.Everybody)
                ctx.Add(new MemberSeason(memberId, family.SeasonId, orderId));

            await ctx.SaveChangesAsync();
        });

        return (family, orderId);
    }

    private Task<int> SeedOtherClubWithAccount(string accountId) => QueryAsync(async ctx =>
    {
        var club = new Club(
            "Other club",
            new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.User], Color.Brown, 2, true, "Single", TimeSpan.FromHours(1), false, false)]);
        ctx.Add(club);
        await ctx.SaveChangesAsync();

        var account = new ClubPaymentAccount(club.Id, accountId, DateTimeOffset.UtcNow);
        account.UpdateStatus(true, true, true);
        ctx.Add(account);
        await ctx.SaveChangesAsync();
        return club.Id;
    });
}
