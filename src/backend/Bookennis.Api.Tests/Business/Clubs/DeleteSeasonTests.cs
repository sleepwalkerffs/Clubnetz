using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class DeleteSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteSeason_RemovesSeasonFromClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));

        await SendAsync(new DeleteSeason(seasonId));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.Seasons).SingleAsync());
        club.Seasons.Should().NotContain(s => s.Id == seasonId);
    }

    [Fact]
    public async Task DeleteSeason_NonExistentSeason_ThrowsInvalidOperationException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var act = () => SendAsync(new DeleteSeason(999999));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
