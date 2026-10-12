using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using OrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;

namespace Bookennis.Api.Tests.Business.Memberships;

public class CancelMembershipOrderTests : TestBase
{
    private readonly IStripeGateway gateway;

    public CancelMembershipOrderTests(TestFixture fixture) : base(fixture)
    {
        gateway = GetInstance<IStripeGateway>();
        gateway.ClearReceivedCalls();
        gateway.IsConfigured.Returns(true);
    }

    [Fact]
    public async Task CancelMembershipOrder_ByMemberBeforePaying_CancelsAndClosesThePaymentPage()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            ctx.Add(new ClubPaymentAccount(TestDataSeed.ClubId, "acct_1", DateTimeOffset.UtcNow));
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            order.StartCheckout("cs_open");
            await ctx.SaveChangesAsync();
            return order.Id;
        });

        var result = await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, orderId, ByTreasurer: false));

        result.Status.Should().Be(OrderStatus.Cancelled);
        result.CanPay.Should().BeFalse();
        await gateway.Received(1).ExpireCheckoutSession("acct_1", "cs_open", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelMembershipOrder_ByMemberAfterPaying_Throws()
    {
        var orderId = await SeedPaidOrder();

        var act = () => SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, orderId, ByTreasurer: false));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
    }

    [Fact]
    public async Task CancelMembershipOrder_ByTreasurerAfterPaying_CancelsButKeepsTheEnrollment()
    {
        var orderId = await SeedPaidOrder();

        var result = await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, orderId, ByTreasurer: true));

        result.Status.Should().Be(OrderStatus.Cancelled);
        (await QueryAsync(ctx => ctx.MemberSeasons.CountAsync())).Should().Be(1, "an order never removes an enrollment by itself");
        await gateway.DidNotReceiveWithAnyArgs().ExpireCheckoutSession(default!, default!, default);
    }

    private Task<int> SeedPaidOrder() => QueryAsync(async ctx =>
    {
        var season = await MembershipSeed.SeedSeason(ctx);
        var plan = await MembershipSeed.SeedPlan(ctx, season.Id);
        var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
        order.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow);
        ctx.Add(new Bookennis.Domain.Members.MemberSeason(TestDataSeed.Member1Id, season.Id, order.Id));
        await ctx.SaveChangesAsync();
        return order.Id;
    });
}
