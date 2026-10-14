using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Payments;

public class SyncMembershipCheckoutTests(TestFixture fixture) : StripeWebhookTestBase(fixture)
{
    [Fact]
    public async Task SyncMembershipCheckout_PaidByCard_BooksThePaymentRightAway_AndTheWebhookAfterwardsChangesNothing()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");

        await SendAsync(new SyncMembershipCheckout(TestDataSeed.ClubId, orderId));

        var order = await Order(orderId);
        order.Status.Should().Be(MembershipOrderStatus.Paid);
        order.PaymentMethod.Should().Be(MembershipPaymentMethod.Online);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);

        // The webhook for the same payment arrives later
        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId);

        (await EnrolledMemberIds()).Should().HaveCount(family.Everybody.Length);
        await Notifier.Received(1).PaymentReceived(Arg.Any<MembershipOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncMembershipCheckout_DebitStarted_TheOrderIsProcessingAndTheMembersAreEnrolled()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "unpaid");

        await SendAsync(new SyncMembershipCheckout(TestDataSeed.ClubId, orderId));

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Processing);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);
    }

    [Fact]
    public async Task SyncMembershipCheckout_PaymentPageStillOpen_ChangesNothing()
    {
        var (_, orderId) = await SeedOrderInCheckout();
        Gateway.GetCheckoutSession(AccountId, SessionId, Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession(SessionId, "https://checkout.stripe.test", "open", "unpaid", orderId.ToString(), TestDataSeed.ClubId.ToString(), null));

        await SendAsync(new SyncMembershipCheckout(TestDataSeed.ClubId, orderId));

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        (await EnrolledMemberIds()).Should().BeEmpty();
    }

    [Fact]
    public async Task SyncMembershipCheckout_SessionOfAnotherOrder_ChangesNothing()
    {
        var (_, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId + 1, "paid");

        await SendAsync(new SyncMembershipCheckout(TestDataSeed.ClubId, orderId));

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        (await EnrolledMemberIds()).Should().BeEmpty();
    }

    [Fact]
    public async Task SyncMembershipCheckout_StripeNotReachable_ChangesNothingAndDoesNotFail()
    {
        var (_, orderId) = await SeedOrderInCheckout();
        Gateway.GetCheckoutSession(AccountId, SessionId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("Stripe is down"));

        await SendAsync(new SyncMembershipCheckout(TestDataSeed.ClubId, orderId));

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
    }

    [Fact]
    public async Task SyncMembershipCheckout_OrderWithoutPaymentPage_DoesNotAskStripe()
    {
        var (_, orderId) = await SeedOrderInCheckout(order => order.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow));

        await SendAsync(new SyncMembershipCheckout(TestDataSeed.ClubId, orderId));

        await Gateway.DidNotReceiveWithAnyArgs().GetCheckoutSession(default!, default!, default);
    }
}
