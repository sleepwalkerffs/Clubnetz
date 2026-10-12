using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Tests.Business.Payments;

public class DisconnectPaymentAccountTests(TestFixture fixture) : PaymentTestBase(fixture)
{
    [Fact]
    public async Task DisconnectPaymentAccount_RemovesTheAccountAndKeepsPaidOrders()
    {
        await SeedAccount();
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await Memberships.MembershipSeed.SeedPlan(ctx, (await Memberships.MembershipSeed.SeedSeason(ctx)).Id);
            var order = await Memberships.MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            order.MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow, paymentIntentId: "pi_1");
            await ctx.SaveChangesAsync();
            return order.Id;
        });

        // Also works when the keys were taken away in the meantime
        Gateway.IsConfigured.Returns(false);
        await SendAsync(new DisconnectPaymentAccount(TestDataSeed.ClubId));

        (await QueryAsync(ctx => ctx.ClubPaymentAccounts.AnyAsync())).Should().BeFalse();
        (await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync(o => o.Id == orderId))).Status.Should().Be(MembershipOrderStatus.Paid);
    }
}
