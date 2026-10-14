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
        (await QueryAsync(ctx => ctx.MemberSeasons.CountAsync())).Should().Be(1, "e.g. the member only gets another plan");
        await gateway.DidNotReceiveWithAnyArgs().ExpireCheckoutSession(default!, default!, default);
    }

    [Fact]
    public async Task CancelMembershipOrder_ByTreasurerWithRemoveEnrollments_TakesOutWhoTheOrderEnrolled_ButNotWhoWasEnrolledByHand()
    {
        var orderId = await SeedPaidOrder();
        var byHand = await QueryAsync(async ctx =>
        {
            var seasonId = await ctx.MembershipOrders.Where(o => o.Id == orderId).Select(o => o.SeasonId).SingleAsync();
            ctx.Add(new Bookennis.Domain.Members.MemberSeason(TestDataSeed.Member2Id, seasonId));
            await ctx.SaveChangesAsync();
            return seasonId;
        });

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, orderId, ByTreasurer: true, RemoveEnrollments: true));

        var enrolled = await QueryAsync(ctx => ctx.MemberSeasons.Where(ms => ms.SeasonId == byHand).Select(ms => ms.MemberId).ToListAsync());
        enrolled.Should().Equal(TestDataSeed.Member2Id);
    }

    [Fact]
    public async Task CancelMembershipOrder_ByMemberAfterTheDebitFailed_TheMemberIsNoLongerEnrolled()
    {
        // An enrollment from a debit that was started; the debit failed and the member withdraws the order
        var orderId = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, season.Id);
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            order.MarkProcessing("pi_1");
            ctx.Add(new Bookennis.Domain.Members.MemberSeason(TestDataSeed.Member1Id, season.Id, order.Id));
            order.MarkFailed();
            await ctx.SaveChangesAsync();
            return order.Id;
        });

        var result = await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, orderId, ByTreasurer: false));

        result.Status.Should().Be(OrderStatus.Cancelled);
        (await QueryAsync(ctx => ctx.MemberSeasons.CountAsync(ms => ms.MemberId == TestDataSeed.Member1Id))).Should().Be(0);
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
