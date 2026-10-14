using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class SortMembershipPlansTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task SortMembershipPlans_SetsTheOrderAndKeepsUnlistedPlansBehind()
    {
        var (seasonId, a, b, c) = await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            var first = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("A"), 1);
            var second = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("B"), 2);
            var third = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("C"), 3);
            return (season.Id, first.Id, second.Id, third.Id);
        });

        await SendAsync(new SortMembershipPlans(TestDataSeed.ClubId, seasonId, [c, a, 999999]));

        var names = await QueryAsync(ctx => ctx.MembershipPlans.Where(p => p.SeasonId == seasonId).OrderBy(p => p.SortOrder).Select(p => p.Name).ToListAsync());
        names.Should().Equal("C", "A", "B");
        b.Should().BePositive();
    }
}
