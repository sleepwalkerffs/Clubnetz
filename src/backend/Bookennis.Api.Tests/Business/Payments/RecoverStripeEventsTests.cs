using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Payments;

public class RecoverStripeEventsTests(TestFixture fixture) : StripeWebhookTestBase(fixture)
{
    private void StripeHasUndelivered(params StripeEventInfo[] events)
        => Gateway.ListUndeliveredEvents(AccountId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(events);

    [Fact]
    public async Task RecoverStripeEvents_PaymentTheWebhookMissed_IsBookedAndTheMembersAreEnrolled()
    {
        var (family, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");
        // The account of the listing counts, not what the event says
        StripeHasUndelivered(new StripeEventInfo("evt_missed", ProcessStripeEvent.CheckoutCompleted, null, SessionId));

        await SendAsync(new RecoverStripeEvents());

        var stored = await QueryAsync(ctx => ctx.StripeWebhookEvents.SingleAsync());
        stored.Should().BeEquivalentTo(new { EventId = "evt_missed", StripeAccountId = AccountId, ObjectId = SessionId }, o => o.ExcludingMissingMembers());
        stored.ProcessedAt.Should().NotBeNull();
        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.Paid);
        (await EnrolledMemberIds()).Should().BeEquivalentTo(family.Everybody);
        await Gateway.Received(1).ListUndeliveredEvents(AccountId, Arg.Is<IReadOnlyCollection<string>>(t => t.Contains(ProcessStripeEvent.CheckoutCompleted)), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverStripeEvents_EventTheAppAlreadyKnows_IsNotHandledAgain()
    {
        var (_, orderId) = await SeedOrderInCheckout();
        StripeHasSession(orderId, "paid");
        await Process(ProcessStripeEvent.CheckoutCompleted, SessionId, eventId: "evt_known");
        StripeHasUndelivered(new StripeEventInfo("evt_known", ProcessStripeEvent.CheckoutCompleted, AccountId, SessionId));

        await SendAsync(new RecoverStripeEvents());

        (await QueryAsync(ctx => ctx.StripeWebhookEvents.CountAsync())).Should().Be(1);
        await Notifier.Received(1).PaymentReceived(Arg.Any<MembershipOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverStripeEvents_StripeNotReachable_DoesNotFail()
    {
        await SeedAccount();
        Gateway.ListUndeliveredEvents(AccountId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Stripe is down"));

        await SendAsync(new RecoverStripeEvents());

        (await QueryAsync(ctx => ctx.StripeWebhookEvents.AnyAsync())).Should().BeFalse();
    }
}
