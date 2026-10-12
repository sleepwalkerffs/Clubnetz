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

public class ConfirmMembershipOrderTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ConfirmMembershipOrder_FreePlan_EnrollsTheMembers()
    {
        var (family, orderId) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Kids up to 8", 0) with { BirthYearFrom = 2018 });
            return (seeded, (await MembershipSeed.SeedOrder(ctx, plan, seeded.Parent, seeded.Kid)).Id);
        });

        var result = await SendAsync(new ConfirmMembershipOrder(TestDataSeed.ClubId, orderId));

        result.Status.Should().Be(OrderStatus.Paid);
        result.PaymentMethod.Should().BeNull();

        var enrollment = await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync());
        enrollment.Should().BeEquivalentTo(new { MemberId = family.Kid, family.SeasonId, MembershipOrderId = orderId }, o => o.ExcludingMissingMembers());

        // Confirming twice changes nothing
        await SendAsync(new ConfirmMembershipOrder(TestDataSeed.ClubId, orderId));
        (await QueryAsync(ctx => ctx.MemberSeasons.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task ConfirmMembershipOrder_PlanWithAPrice_Throws()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            return (await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
        });

        var act = () => SendAsync(new ConfirmMembershipOrder(TestDataSeed.ClubId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ConfirmMembershipOrder.ErrorCode.MembershipOrderNotFree));
        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmMembershipOrder_FreePlanThatNeedsConfirmation_WaitsForTheTreasurer()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Honorary", 0) with { RequiresApproval = true });
            return (await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
        });

        var act = () => SendAsync(new ConfirmMembershipOrder(TestDataSeed.ClubId, orderId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipOrder.ErrorCode.MembershipOrderInvalidStatus));
        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse();
    }
}
