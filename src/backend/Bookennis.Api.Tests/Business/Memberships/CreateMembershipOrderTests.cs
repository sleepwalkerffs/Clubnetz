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

public class CreateMembershipOrderTests : TestBase
{
    private readonly IMembershipNotifier notifier;

    public CreateMembershipOrderTests(TestFixture fixture) : base(fixture)
    {
        notifier = GetInstance<IMembershipNotifier>();
        notifier.ClearReceivedCalls();
    }

    [Fact]
    public async Task CreateMembershipOrder_ForOneself_CreatesAnOrderAwaitingPayment()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        var result = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        result.Should().BeEquivalentTo(new { PlanName = "Adult", AmountCents = 12000, Status = OrderStatus.AwaitingPayment, PayerMemberId = TestDataSeed.Member1Id, CanPay = true }, o => o.ExcludingMissingMembers());

        var order = await QueryAsync(ctx => ctx.MembershipOrders.Include(o => o.Members).SingleAsync());
        order.Id.Should().Be(result.Id);
        order.ClubId.Should().Be(TestDataSeed.ClubId);
        order.Members.Select(m => m.MemberId).Should().Equal(TestDataSeed.Member1Id);

        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse("ordering does not enroll, paying does");
        await notifier.DidNotReceiveWithAnyArgs().NeedsAttention(default!, default, default);
    }

    [Fact]
    public async Task CreateMembershipOrder_PlanThatNeedsConfirmation_WaitsForTheTreasurerAndTellsThem()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Student", 7000) with { RequiresApproval = true })).Id);

        var result = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        result.Status.Should().Be(OrderStatus.AwaitingApproval);
        result.CanPay.Should().BeFalse();
        await notifier.Received(1).NeedsAttention(Arg.Is<MembershipOrder>(o => o.Id == result.Id), MembershipAttentionReason.ApprovalRequested, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateMembershipOrder_ParentForTheFamily_CoversEverybody()
    {
        var (family, planId) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            return (seeded, (await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.FamilyData())).Id);
        });

        var result = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, family.Everybody));

        result.AmountCents.Should().Be(21500, "the price of a family plan is per order, not per member");
        result.Members.Select(m => m.MemberId).Should().BeEquivalentTo(family.Everybody);
        result.PayerName.Should().Be("us er");
    }

    [Fact]
    public async Task CreateMembershipOrder_ForSomebodyOutsideTheOwnFamily_IsDenied()
    {
        var (planId, strangerId) = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            return (plan.Id, await MembershipSeed.AddMember(ctx, "Stranger"));
        });

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [strangerId]));

        await act.Should().ThrowAsync<AccessDeniedException>();
        (await QueryAsync(ctx => ctx.MembershipOrders.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task CreateMembershipOrder_ChildForItsParent_IsDenied()
    {
        var (planId, teenUserId) = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            var teen = await MembershipSeed.AddMember(ctx, "Teen", 2010);
            await MembershipSeed.AddFamily(ctx, [TestDataSeed.Member1Id], teen);
            return (plan.Id, await ctx.ClubMembers.Where(m => m.Id == teen).Select(m => m.UserId).SingleAsync());
        });

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, teenUserId, planId, [TestDataSeed.Member1Id]));

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task CreateMembershipOrder_RuleOfThePlanBroken_ThrowsTheReason()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Senior") with { BirthYearTo = 1956 })).Id);

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.BirthYearOutOfRange));
    }

    [Fact]
    public async Task CreateMembershipOrder_NewMemberPlanForSomebodyEnrolledBefore_Throws()
    {
        var planId = await QueryAsync(async ctx =>
        {
            var lastSeason = await MembershipSeed.SeedSeason(ctx, MembershipSeed.Today.AddDays(-400), MembershipSeed.Today.AddDays(-200));
            await MembershipSeed.Enroll(ctx, lastSeason.Id, TestDataSeed.Member1Id);
            return (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("New member") with { NewMembersOnly = true })).Id;
        });

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.NotANewMember));
    }

    [Fact]
    public async Task CreateMembershipOrder_PartnerCard_NeedsThePartnerToBeEnrolledOrPaid()
    {
        var (family, planId, adultPlan) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var adult = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId);
            var partnerCard = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Partner card", 8000) with { RequiresEnrolledFamilyAdult = true }, 2);
            return (seeded, partnerCard.Id, adult);
        });

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [family.Parent]));
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.NoEnrolledFamilyAdult));

        // An order of the partner that is not paid yet does not count
        var partnerOrderId = await QueryAsync(async ctx => (await MembershipSeed.SeedOrder(ctx, adultPlan, family.Partner, family.Partner)).Id);
        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.NoEnrolledFamilyAdult));

        await QueryAsync(async ctx =>
        {
            var order = await ctx.MembershipOrders.SingleAsync(o => o.Id == partnerOrderId);
            order.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [family.Parent]));
        result.PlanName.Should().Be("Partner card");
    }

    [Fact]
    public async Task CreateMembershipOrder_SecondOrderInTheSeason_Throws()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);
        await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
        (await QueryAsync(ctx => ctx.MembershipOrders.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task CreateMembershipOrder_AfterTheFirstOrderWasCancelled_IsAllowedAgain()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);
        var first = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));
        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, first.Id, ByTreasurer: false));

        var second = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task CreateMembershipOrder_MemberEnrolledByHand_CanStillOrder()
    {
        var planId = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            await MembershipSeed.Enroll(ctx, season.Id, TestDataSeed.Member1Id);
            return (await MembershipSeed.SeedPlan(ctx, season.Id)).Id;
        });

        var result = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        result.Status.Should().Be(OrderStatus.AwaitingPayment, "a club may enroll everybody first and collect the fees afterwards");
    }

    [Fact]
    public async Task CreateMembershipOrder_PlanOnlyTheTreasurerAssigns_Throws()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Honorary", 0) with { IsSelfService = false })).Id);

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateMembershipOrder.ErrorCode.MembershipPlanNotSelfService));
    }

    [Fact]
    public async Task CreateMembershipOrder_PlanOfAnotherClub_IsNotFound()
    {
        var planId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id)).Id);

        var act = () => SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId + 1, TestDataSeed.UserId, planId, [TestDataSeed.Member1Id]));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
