using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class GetMemberBadgesTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMemberBadges_ReturnsTheTierBadgesWithTheirSport()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var padel = new BadgeTier(TestDataSeed.ClubId, season.Id, 1, "Padel Bronze", "", 1, 1, Sport.Padel);
            var all = new BadgeTier(TestDataSeed.ClubId, season.Id, 1, "Bronze", "", 1, 1);
            ctx.AddRange(padel, all);
            await ctx.SaveChangesAsync();

            ctx.AddRange(new MemberBadge(TestDataSeed.Member1Id, padel.Id, season.Id), new MemberBadge(TestDataSeed.Member1Id, all.Id, season.Id));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        var result = await SendAsync(new GetMemberBadges(TestDataSeed.Member1Id, TestDataSeed.ClubId, seasonId));

        result.Badges.Select(b => (b.Name, b.Sport)).Should().Equal(
            ("Bronze", (Bookennis.Shared.Controller.Shared.Sport?)null),
            ("Padel Bronze", Bookennis.Shared.Controller.Shared.Sport.Padel));
    }
}
