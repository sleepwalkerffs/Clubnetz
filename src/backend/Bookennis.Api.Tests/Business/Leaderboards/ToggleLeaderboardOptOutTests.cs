using Bookennis.Api.Business.Leaderboards;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Leaderboards;

public class ToggleLeaderboardOptOutTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ToggleOptOut_OptIn_SetsLeaderboardOptOut()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;

            ctx.Add(new MemberSeason(member1.Id, seasonId));
            await ctx.SaveChangesAsync();
        });

        await SendAsync(new ToggleLeaderboardOptOut(userId, seasonId));

        var isOptedOut = await QueryAsync(async ctx =>
            await ctx.MemberSeasons.Where(ms => ms.SeasonId == seasonId).Select(ms => ms.LeaderboardOptOut).FirstAsync());
        isOptedOut.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleOptOut_AlreadyOptedOut_ClearsLeaderboardOptOut()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;

            var memberSeason = new MemberSeason(member1.Id, seasonId);
            memberSeason.LeaderboardOptOut = true;
            ctx.Add(memberSeason);
            await ctx.SaveChangesAsync();
        });

        await SendAsync(new ToggleLeaderboardOptOut(userId, seasonId));

        var isOptedOut = await QueryAsync(async ctx =>
            await ctx.MemberSeasons.Where(ms => ms.SeasonId == seasonId).Select(ms => ms.LeaderboardOptOut).FirstAsync());
        isOptedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleOptOut_DoubleToggle_RestoresOriginalState()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;

            ctx.Add(new MemberSeason(member1.Id, seasonId));
            await ctx.SaveChangesAsync();
        });

        await SendAsync(new ToggleLeaderboardOptOut(userId, seasonId));
        await SendAsync(new ToggleLeaderboardOptOut(userId, seasonId));

        var isOptedOut = await QueryAsync(async ctx =>
            await ctx.MemberSeasons.Where(ms => ms.SeasonId == seasonId).Select(ms => ms.LeaderboardOptOut).FirstAsync());
        isOptedOut.Should().BeFalse();
    }
}
