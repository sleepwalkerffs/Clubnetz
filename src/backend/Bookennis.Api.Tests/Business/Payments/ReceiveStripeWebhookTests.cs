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

public class ReceiveStripeWebhookTests(TestFixture fixture) : StripeWebhookTestBase(fixture)
{
    private const string Payload = """{"id":"evt_1"}""";

    [Fact]
    public async Task ReceiveStripeWebhook_WrongSignature_ThrowsAndStoresNothing()
    {
        Gateway.ParseWebhookEvent(Payload, "forged").Throws(new StripeSignatureException("The webhook request could not be verified."));

        var act = () => SendAsync(new ReceiveStripeWebhook(Payload, "forged"));

        await act.Should().ThrowAsync<StripeSignatureException>();
        (await QueryAsync(ctx => ctx.StripeWebhookEvents.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveStripeWebhook_PaidCheckout_StoresTheEventAndEnrollsTheMembers()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");
        Gateway.ParseWebhookEvent(Payload, "valid").Returns(new StripeEventInfo("evt_1", ProcessStripeEvent.CheckoutCompleted, AccountId, SessionId));

        await SendAsync(new ReceiveStripeWebhook(Payload, "valid"));

        var stored = await QueryAsync(ctx => ctx.StripeWebhookEvents.SingleAsync());
        stored.Should().BeEquivalentTo(new { EventId = "evt_1", Type = "checkout.session.completed", StripeAccountId = AccountId, ObjectId = SessionId }, o => o.ExcludingMissingMembers());
        stored.ProcessedAt.Should().NotBeNull();

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Paid);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);
    }

    [Fact]
    public async Task ReceiveStripeWebhook_SameEventTwice_IsHandledOnce()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");
        Gateway.ParseWebhookEvent(Payload, "valid").Returns(new StripeEventInfo("evt_1", ProcessStripeEvent.CheckoutCompleted, AccountId, SessionId));

        await SendAsync(new ReceiveStripeWebhook(Payload, "valid"));
        await SendAsync(new ReceiveStripeWebhook(Payload, "valid"));

        (await QueryAsync(ctx => ctx.StripeWebhookEvents.CountAsync())).Should().Be(1);
        (await EnrolledMemberIds()).Should().HaveCount(family.Everybody.Length, "one enrollment per member");
        await Gateway.Received(1).GetCheckoutSession(AccountId, SessionId, Arg.Any<CancellationToken>());
        await Notifier.Received(1).PaymentReceived(Arg.Any<MembershipOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveStripeWebhook_EventTheAppDoesNotActOn_IsNotStored()
    {
        Gateway.ParseWebhookEvent(Payload, "valid").Returns(new StripeEventInfo("evt_1", "customer.created", AccountId, "cus_1"));

        await SendAsync(new ReceiveStripeWebhook(Payload, "valid"));

        (await QueryAsync(ctx => ctx.StripeWebhookEvents.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveStripeWebhook_OverlongIds_AreRejected()
    {
        Gateway.ParseWebhookEvent(Payload, "valid").Returns(new StripeEventInfo(new string('e', 300), ProcessStripeEvent.CheckoutCompleted, AccountId, SessionId));

        var act = () => SendAsync(new ReceiveStripeWebhook(Payload, "valid"));

        await act.Should().ThrowAsync<StripeSignatureException>();
    }
}
