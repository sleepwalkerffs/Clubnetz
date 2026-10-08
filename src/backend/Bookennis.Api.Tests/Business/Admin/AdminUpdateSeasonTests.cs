using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminUpdateSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminUpdateSeason_UpdatesPeriod()
    {
        var (clubId, seasonId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2029, 4, 1), new DateOnly(2029, 9, 30)));
            await ctx.SaveChangesAsync();
            return (club.Id, season.Id);
        });

        await SendAsync(new AdminUpdateSeason(clubId, seasonId, new DateOnly(2029, 5, 1), new DateOnly(2029, 10, 31)));

        var season = await QueryAsync(ctx => ctx.Seasons.SingleAsync(s => s.Id == seasonId));
        season.Period.From.Should().Be(new DateOnly(2029, 5, 1));
        season.Period.To.Should().Be(new DateOnly(2029, 10, 31));
    }
}
