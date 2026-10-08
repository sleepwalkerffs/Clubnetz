using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class GetSeasonsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetSeasons_ReturnsSeasonsForClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));

        var result = await SendAsync(new GetSeasons());

        result.Seasons.Should().Contain(s => s.Id == seasonId);
        var season = result.Seasons.Single(s => s.Id == seasonId);
        season.StartDate.Should().Be(new DateOnly(2025, 4, 1));
        season.EndDate.Should().Be(new DateOnly(2025, 11, 30));
    }

    [Fact]
    public async Task GetSeasons_ReturnsIsActiveCorrectly()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeSeasonId = await SendAsync(new AddSeason(today.AddDays(-30), today.AddDays(30)));
        var inactiveSeasonId = await SendAsync(new AddSeason(today.AddDays(60), today.AddDays(120)));

        var result = await SendAsync(new GetSeasons());

        result.Seasons.Single(s => s.Id == activeSeasonId).IsActive.Should().BeTrue();
        result.Seasons.Single(s => s.Id == inactiveSeasonId).IsActive.Should().BeFalse();
    }
}
