using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class CreateMembershipPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateMembershipPlan_AddsThePlanAtTheEnd()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data(), sortOrder: 4);
            return season.Id;
        });

        var planId = await SendAsync(new CreateMembershipPlan(TestDataSeed.ClubId, seasonId, MembershipSeed.FamilyData() with { Description = " One price for everybody " }));

        var plan = await QueryAsync(ctx => ctx.MembershipPlans.SingleAsync(p => p.Id == planId));
        plan.Should().BeEquivalentTo(new { ClubId = TestDataSeed.ClubId, SeasonId = seasonId, Name = "Family", Description = "One price for everybody", PriceCents = 21500, MaxAdults = 2, IncludesChildren = true, SortOrder = 5 }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task CreateMembershipPlan_NameAlreadyUsedInTheSeason_Throws()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            await MembershipSeed.SeedPlan(ctx, season.Id);
            return season.Id;
        });

        var act = () => SendAsync(new CreateMembershipPlan(TestDataSeed.ClubId, seasonId, MembershipSeed.Data()));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateMembershipPlan.ErrorCode.MembershipPlanNameAlreadyUsed));
    }

    [Fact]
    public async Task CreateMembershipPlan_SameNameInAnotherSeason_IsAllowed()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var other = await MembershipSeed.SeedSeason(ctx, new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31));
            await MembershipSeed.SeedPlan(ctx, other.Id);
            return (await MembershipSeed.SeedSeason(ctx)).Id;
        });

        var planId = await SendAsync(new CreateMembershipPlan(TestDataSeed.ClubId, seasonId, MembershipSeed.Data()));

        planId.Should().BePositive();
    }

    [Fact]
    public async Task CreateMembershipPlan_InvalidData_ThrowsTheErrorOfTheDomain()
    {
        var seasonId = await QueryAsync(async ctx => (await MembershipSeed.SeedSeason(ctx)).Id);

        var act = () => SendAsync(new CreateMembershipPlan(TestDataSeed.ClubId, seasonId, MembershipSeed.Data() with { Name = "" }));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipPlan.ErrorCode.MembershipPlanNameRequired));
    }

    [Fact]
    public async Task CreateMembershipPlan_SeasonOfAnotherClub_IsNotFound()
    {
        var seasonId = await QueryAsync(async ctx => (await MembershipSeed.SeedSeason(ctx)).Id);

        var act = () => SendAsync(new CreateMembershipPlan(TestDataSeed.ClubId + 1, seasonId, MembershipSeed.Data()));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
