using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class GetMembershipPlansTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMembershipPlans_Member_GetsSelfServicePlansOfTheSeasonInTheirOrder()
    {
        var (seasonId, planId) = await Seed();

        var result = await SendAsync(new GetMembershipPlans(TestDataSeed.ClubId, seasonId, CanManage: false));

        result.Plans.Select(p => p.Name).Should().Equal("Youth", "Adult");
        result.Plans.Should().OnlyContain(p => p.OrderCount == 0, "the number of orders is for treasurers");

        var youth = result.Plans[0];
        youth.Should().BeEquivalentTo(new { SeasonId = seasonId, PriceCents = 6000, Currency = "eur", BirthYearFrom = 2009, BirthYearTo = 2013, IsSelfService = true, GrantsSeasonEnrollment = true, MaxAdults = 1 }, o => o.ExcludingMissingMembers());
        result.Plans[1].Id.Should().Be(planId);
    }

    [Fact]
    public async Task GetMembershipPlans_Treasurer_AlsoGetsPlansOnlyTheyAssignAndTheOrderCounts()
    {
        var (seasonId, planId) = await Seed();

        var result = await SendAsync(new GetMembershipPlans(TestDataSeed.ClubId, seasonId, CanManage: true));

        result.Plans.Select(p => p.Name).Should().Equal("Youth", "Adult", "Honorary member");
        result.Plans.Single(p => p.Id == planId).OrderCount.Should().Be(1);
        result.Plans.Single(p => p.Name == "Honorary member").IsSelfService.Should().BeFalse();
    }

    private Task<(int SeasonId, int AdultPlanId)> Seed() => QueryAsync(async ctx =>
    {
        var season = await MembershipSeed.SeedSeason(ctx);
        var otherSeason = await MembershipSeed.SeedSeason(ctx, new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31));

        var adult = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data(), sortOrder: 2);
        await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Youth", 6000) with { BirthYearFrom = 2009, BirthYearTo = 2013 }, sortOrder: 1);
        await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Honorary member", 0) with { IsSelfService = false }, sortOrder: 3);
        await MembershipSeed.SeedPlan(ctx, otherSeason.Id, MembershipSeed.Data("Old plan"));
        await MembershipSeed.SeedOrder(ctx, adult, TestDataSeed.Member1Id, TestDataSeed.Member1Id);

        return (season.Id, adult.Id);
    });
}
