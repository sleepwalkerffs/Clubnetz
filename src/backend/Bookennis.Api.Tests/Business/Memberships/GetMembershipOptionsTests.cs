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

public class GetMembershipOptionsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMembershipOptions_NoSeason_ReturnsAnEmptyResult()
    {
        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, null));

        result.SeasonId.Should().BeNull();
        result.Plans.Should().BeEmpty();
        result.People.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembershipOptions_WithoutSeasonId_UsesTheRunningSeasonElseTheNextOne()
    {
        var (running, next) = await QueryAsync(async ctx =>
        {
            await MembershipSeed.SeedSeason(ctx, MembershipSeed.Today.AddDays(-800), MembershipSeed.Today.AddDays(-500));
            var nextSeason = await MembershipSeed.SeedSeason(ctx, MembershipSeed.Today.AddDays(300), MembershipSeed.Today.AddDays(500));
            var runningSeason = await MembershipSeed.SeedSeason(ctx);
            return (runningSeason, nextSeason);
        });

        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, null));
        result.SeasonId.Should().Be(running.Id);
        result.SeasonStart.Should().Be(running.Period.From);
        result.SeasonEnd.Should().Be(running.Period.To);

        var explicitResult = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, next.Id));
        explicitResult.SeasonId.Should().Be(next.Id);
    }

    [Fact]
    public async Task GetMembershipOptions_MemberWithoutFamily_SeesThePlansWithTheReasonsWhySomeCanNotBeChosen()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data(), 1);
            await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Senior", 9000) with { BirthYearTo = 1956 }, 2);
            await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Partner card", 8000) with { RequiresEnrolledFamilyAdult = true }, 3);
            await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Honorary", 0) with { IsSelfService = false }, 4);
            return season.Id;
        });

        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, seasonId));

        result.OnlinePaymentAvailable.Should().BeFalse();
        result.People.Should().ContainSingle().Which.Should().BeEquivalentTo(new { MemberId = TestDataSeed.Member1Id, FirstName = "us", IsMe = true, IsChild = false, IsEnrolled = false, HasMembership = false }, o => o.ExcludingMissingMembers());

        result.Plans.Select(p => p.Plan.Name).Should().Equal("Adult", "Senior", "Partner card");
        result.Plans[0].IsEligible.Should().BeTrue();
        result.Plans[0].Choices.Should().ContainSingle().Which.MemberIds.Should().Equal(TestDataSeed.Member1Id);
        result.Plans[1].Choices.Single().Reasons.Should().Equal("BirthYearOutOfRange");
        result.Plans[2].Choices.Single().Reasons.Should().Equal("NoEnrolledFamilyAdult");
    }

    [Fact]
    public async Task GetMembershipOptions_Parent_CanChooseForTheFamily()
    {
        var family = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Youth", 6000) with { BirthYearFrom = 2009, BirthYearTo = 2013 }, 1);
            await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.FamilyData(adults: 1, 18000) with { Name = "Family (1 adult)" }, 2);
            await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.FamilyData(adults: 2), 3);
            await MembershipSeed.SeedPlan(ctx, seeded.SeasonId, MembershipSeed.Data("Couple", 20000) with { MaxAdults = 2 }, 4);
            return seeded;
        });

        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, family.SeasonId));

        result.People.Select(p => p.MemberId).Should().BeEquivalentTo(family.Everybody);
        result.People[0].IsMe.Should().BeTrue();
        result.People.Where(p => p.IsChild).Select(p => p.MemberId).Should().BeEquivalentTo([family.Teen, family.Kid]);

        // A plan for one member is offered per person
        var youth = result.Plans[0];
        youth.Choices.Should().HaveCount(4);
        youth.Choices.Where(c => c.IsEligible).Should().ContainSingle().Which.MemberIds.Should().Equal(family.Teen);

        // A family plan is offered once, with everybody it can cover
        result.Plans[1].Choices.Should().ContainSingle().Which.Should().BeEquivalentTo(new { MemberIds = new[] { family.Parent, family.Teen, family.Kid }, IsEligible = true }, o => o.ExcludingMissingMembers());
        result.Plans[2].Choices.Should().ContainSingle().Which.MemberIds.Should().BeEquivalentTo(family.Everybody);
        result.Plans[3].Choices.Should().ContainSingle().Which.MemberIds.Should().BeEquivalentTo([family.Parent, family.Partner]);
    }

    [Fact]
    public async Task GetMembershipOptions_Child_OnlySeesItself()
    {
        var (seasonId, teenUserId, teenMemberId) = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            var teen = await MembershipSeed.AddMember(ctx, "Teen", 2010);
            await MembershipSeed.AddFamily(ctx, [TestDataSeed.Member1Id], teen);
            await MembershipSeed.SeedPlan(ctx, season.Id);
            var userId = await ctx.ClubMembers.Where(m => m.Id == teen).Select(m => m.UserId).SingleAsync();
            return (season.Id, userId, teen);
        });

        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, teenUserId, seasonId));

        result.People.Should().ContainSingle().Which.MemberId.Should().Be(teenMemberId);
        result.Plans.Single().Choices.Should().ContainSingle().Which.MemberIds.Should().Equal(teenMemberId);
    }

    [Fact]
    public async Task GetMembershipOptions_ExistingOrder_IsReturnedAndBlocksAnotherMembership()
    {
        var (family, orderId) = await QueryAsync(async ctx =>
        {
            var seeded = await MembershipSeed.SeedFamily(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, seeded.SeasonId);
            var order = await MembershipSeed.SeedOrder(ctx, plan, seeded.Partner, seeded.Partner);

            // An order of somebody the member has nothing to do with
            var stranger = await MembershipSeed.AddMember(ctx, "Stranger");
            await MembershipSeed.SeedOrder(ctx, plan, stranger, stranger);
            return (seeded, order.Id);
        });

        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, family.SeasonId));

        var order = result.Orders.Should().ContainSingle().Subject;
        order.Should().BeEquivalentTo(new { Id = orderId, PlanName = "Adult", AmountCents = 12000, Status = OrderStatus.AwaitingPayment, PayerName = "Partner Tester", CanPay = true, CanCancel = true }, o => o.ExcludingMissingMembers());
        order.Members.Should().ContainSingle().Which.MemberId.Should().Be(family.Partner);

        result.People.Single(p => p.MemberId == family.Partner).HasMembership.Should().BeTrue();
        var choices = result.Plans.Single().Choices;
        choices.Single(c => c.MemberIds.Contains(family.Partner)).Reasons.Should().Equal("AlreadyHasMembership");
        choices.Single(c => c.MemberIds.Contains(family.Parent)).IsEligible.Should().BeTrue();
    }

    [Fact]
    public async Task GetMembershipOptions_ClubThatTakesOnlinePayments_SaysSo()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var account = new ClubPaymentAccount(TestDataSeed.ClubId, "acct_1", DateTimeOffset.UtcNow);
            account.UpdateStatus(chargesEnabled: true, payoutsEnabled: true, detailsSubmitted: true);
            ctx.Add(account);
            return (await MembershipSeed.SeedSeason(ctx)).Id;
        });
        GetInstance<IStripeGateway>().IsConfigured.Returns(true);

        var result = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, seasonId));

        result.OnlinePaymentAvailable.Should().BeTrue();
        GetInstance<IStripeGateway>().IsConfigured.Returns(false);
    }

    [Fact]
    public async Task GetMembershipOptions_UserWithoutAClubMember_IsNotFound()
    {
        var act = () => SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, 987654, null));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
