using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Tests.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Payments;

public class StartMembershipCheckoutTests(TestFixture fixture) : PaymentTestBase(fixture)
{
    [Fact]
    public async Task StartMembershipCheckout_CreatesASessionOnTheAccountOfTheClubAndReturnsItsAddress()
    {
        await SeedAccount();
        var (family, orderId) = await SeedFamilyOrder();
        StripeCheckoutData? sent = null;
        Gateway.CreateCheckoutSession(AccountId, Arg.Do<StripeCheckoutData>(d => sent = d), Arg.Any<CancellationToken>())
            .Returns(Session("cs_1"));

        var result = await SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        result.Url.Should().Be("https://checkout.stripe.test/cs_1");

        sent.Should().NotBeNull();
        sent!.ClubId.Should().Be(TestDataSeed.ClubId);
        sent.OrderId.Should().Be(orderId);
        sent.ProductName.Should().StartWith("Family · ");
        sent.Description.Should().Be("us er, Partner Tester, Teen Tester, Kid Tester");
        sent.AmountCents.Should().Be(21500);
        sent.Currency.Should().Be("eur");
        sent.CustomerEmail.Should().Be("user@bookennis.com");
        sent.Locale.Should().Be("de");
        sent.ApplicationFeeCents.Should().BeNull("the platform fee is 0 unless the operator sets one");
        sent.SuccessUrl.Should().Be($"http://notvalid.com/clubs/{TestDataSeed.ClubId}/membership?season={family.SeasonId}&order={orderId}&checkout=success");
        sent.CancelUrl.Should().EndWith("&checkout=cancelled");

        var order = await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync());
        order.StripeCheckoutSessionId.Should().Be("cs_1");
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment, "only the webhook says that an order is paid");
        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task StartMembershipCheckout_SecondTime_ClosesTheFirstPaymentPage()
    {
        await SeedAccount();
        var (_, orderId) = await SeedFamilyOrder(order => order.StartCheckout("cs_old"));
        Gateway.CreateCheckoutSession(AccountId, Arg.Any<StripeCheckoutData>(), Arg.Any<CancellationToken>()).Returns(Session("cs_new"));

        await SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        await Gateway.Received(1).ExpireCheckoutSession(AccountId, "cs_old", Arg.Any<CancellationToken>());
        (await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync())).StripeCheckoutSessionId.Should().Be("cs_new");
    }

    [Fact]
    public async Task StartMembershipCheckout_InstallationWithoutKeys_Throws()
    {
        Gateway.IsConfigured.Returns(false);
        var (_, orderId) = await SeedFamilyOrder();

        var act = () => SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentsNotConfigured));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartMembershipCheckout_ClubCanNotTakePayments_Throws(bool connected)
    {
        if (connected)
            await SeedAccount(chargesEnabled: false);

        var (_, orderId) = await SeedFamilyOrder();

        var act = () => SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentAccountNotReady));
        await Gateway.DidNotReceiveWithAnyArgs().CreateCheckoutSession(default!, default!, default);
    }

    [Fact]
    public async Task StartMembershipCheckout_FreePlan_Throws()
    {
        await SeedAccount();
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Kids", 0));
            return (await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
        });

        var act = () => SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(StartMembershipCheckout.ErrorCode.MembershipOrderIsFree));
    }

    [Theory]
    [InlineData(MembershipOrderStatus.AwaitingApproval)]
    [InlineData(MembershipOrderStatus.Paid)]
    [InlineData(MembershipOrderStatus.Processing)]
    [InlineData(MembershipOrderStatus.Cancelled)]
    public async Task StartMembershipCheckout_OrderThatCanNotBePaidNow_ThrowsBeforeAskingStripe(MembershipOrderStatus status)
    {
        await SeedAccount();
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data() with { RequiresApproval = status == MembershipOrderStatus.AwaitingApproval });
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            switch (status)
            {
                case MembershipOrderStatus.Paid: order.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow); break;
                case MembershipOrderStatus.Processing: order.MarkProcessing("pi_1"); break;
                case MembershipOrderStatus.Cancelled: order.CancelByMember(); break;
            }

            await ctx.SaveChangesAsync();
            return order.Id;
        });

        var act = () => SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
        await Gateway.DidNotReceiveWithAnyArgs().CreateCheckoutSession(default!, default!, default);
    }

    [Fact]
    public async Task StartMembershipCheckout_FailedOrder_CanBePaidAgain()
    {
        await SeedAccount();
        var (_, orderId) = await SeedFamilyOrder(order => order.MarkFailed());
        Gateway.CreateCheckoutSession(AccountId, Arg.Any<StripeCheckoutData>(), Arg.Any<CancellationToken>()).Returns(Session("cs_retry"));

        await SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        var order = await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync());
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        order.StripeCheckoutSessionId.Should().Be("cs_retry");
    }

    [Fact]
    public async Task StartMembershipCheckout_FailedOrderWhoseMemberGotAnotherMembership_Throws()
    {
        await SeedAccount();
        var (family, orderId) = await SeedFamilyOrder(order => order.MarkFailed());
        await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, family.SeasonId, MembershipSeed.Data(), 2);
            await MembershipSeed.SeedOrder(ctx, plan, family.Teen, family.Teen);
        });

        var act = () => SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
        await Gateway.DidNotReceiveWithAnyArgs().CreateCheckoutSession(default!, default!, default);
    }

    [Fact]
    public async Task StartMembershipCheckout_StripeDeclines_ThrowsALocalizedErrorAndKeepsTheOrder()
    {
        await SeedAccount();
        var (_, orderId) = await SeedFamilyOrder();
        Gateway.CreateCheckoutSession(AccountId, Arg.Any<StripeCheckoutData>(), Arg.Any<CancellationToken>())
            .Returns<StripeCheckoutSession>(_ => throw new global::Stripe.StripeException("No such account"));

        var act = () => SendAsync(new StartMembershipCheckout(TestDataSeed.ClubId, TestDataSeed.UserId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PaymentErrorCode.PaymentProviderUnavailable));
        (await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync())).StripeCheckoutSessionId.Should().BeNull();
    }

    [Theory]
    [InlineData(21500, 0, null)]
    [InlineData(21500, 1.5, 322L)]
    [InlineData(21500, 100, 21500L)]
    [InlineData(21500, 250, 21500L)]
    [InlineData(50, 1, null)]
    public void ApplicationFee_IsTheConfiguredShareRoundedDown(int amountCents, double percent, long? expected)
    {
        StartMembershipCheckout.Handler.ApplicationFee(amountCents, (decimal)percent).Should().Be(expected);
    }

    private static StripeCheckoutSession Session(string id) => new(id, $"https://checkout.stripe.test/{id}", "open", "unpaid", null, null, null);

    private Task<(SeededFamily Family, int OrderId)> SeedFamilyOrder(Action<MembershipOrder>? change = null) => QueryAsync(async ctx =>
    {
        var family = await MembershipSeed.SeedFamily(ctx);
        var plan = await MembershipSeed.SeedPlan(ctx, family.SeasonId, MembershipSeed.FamilyData());
        var order = await MembershipSeed.SeedOrder(ctx, plan, family.Parent, family.Everybody);
        change?.Invoke(order);
        await ctx.SaveChangesAsync();
        return (family, order.Id);
    });
}
