using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminDeleteSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminDeleteSeason_RemovesSeason()
    {
        var (clubId, seasonId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2028, 4, 1), new DateOnly(2028, 9, 30)));
            await ctx.SaveChangesAsync();
            return (club.Id, season.Id);
        });

        await SendAsync(new AdminDeleteSeason(clubId, seasonId));

        var exists = await QueryAsync(ctx => ctx.Seasons.AnyAsync(s => s.Id == seasonId));
        exists.Should().BeFalse();
    }
}
