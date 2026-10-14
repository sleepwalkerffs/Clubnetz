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

public abstract class StripeWebhookTestBase : PaymentTestBase
{
    protected const string SessionId = "cs_1";
    protected const string PaymentIntentId = "pi_1";

    protected StripeWebhookTestBase(TestFixture fixture) : base(fixture)
    {
        Notifier = GetInstance<IMembershipNotifier>();
        Notifier.ClearReceivedCalls();
    }

    protected IMembershipNotifier Notifier { get; }

    /// <summary>The family of Member1 with an order of a family plan whose payment page is open, in a club that takes payments.</summary>
    protected async Task<(SeededFamily Family, int OrderId)> SeedOrderInCheckout(Action<MembershipOrder>? change = null)
    {
        await SeedAccount();
        return await QueryAsync(async ctx =>
        {
            var family = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, family.SeasonId, MembershipSeed.FamilyData());
            var order = await MembershipSeed.SeedOrder(ctx, plan, family.Parent, family.Everybody);
            order.StartCheckout(SessionId);
            change?.Invoke(order);
            await ctx.SaveChangesAsync();
            return (family, order.Id);
        });
    }

    protected void StripeHasSession(int orderId, string paymentStatus, string accountId = AccountId)
        => Gateway.GetCheckoutSession(accountId, SessionId, Arg.Any<CancellationToken>())
            .Returns(new StripeCheckoutSession(SessionId, null, "complete", paymentStatus, orderId.ToString(), TestDataSeed.ClubId.ToString(), PaymentIntentId));

    /// <summary>Stores an event like the webhook endpoint does and processes it.</summary>
    protected async Task Process(string type, string objectId, string? accountId = AccountId, string? eventId = null)
    {
        var id = await QueryAsync(async ctx =>
        {
            var stripeEvent = new StripeWebhookEvent(eventId ?? $"evt_{Guid.NewGuid():N}", type, accountId, objectId, DateTimeOffset.UtcNow);
            ctx.Add(stripeEvent);
            await ctx.SaveChangesAsync();
            return stripeEvent.Id;
        });

        await SendAsync(new ProcessStripeEvent(id));
    }

    protected Task<MembershipOrder> Order(int orderId) => QueryAsync(ctx => ctx.MembershipOrders.Include(o => o.Members).SingleAsync(o => o.Id == orderId));

    protected Task<List<int>> EnrolledMemberIds() => QueryAsync(ctx => ctx.MemberSeasons.Select(ms => ms.MemberId).ToListAsync());
}
