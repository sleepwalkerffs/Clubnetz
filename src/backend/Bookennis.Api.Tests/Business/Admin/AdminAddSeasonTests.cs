using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AdminAddSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AdminAddSeason_AddsSeason()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var seasonId = await SendAsync(new AdminAddSeason(clubId, new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30)));

        var season = await QueryAsync(ctx => ctx.Seasons.SingleOrDefaultAsync(s => s.Id == seasonId));
        season.Should().NotBeNull();
        season!.ClubId.Should().Be(clubId);
    }

    [Fact]
    public async Task AdminAddSeason_OverlappingSeason_Throws()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            club.AddSeason(new DateOnlyInterval(new DateOnly(2027, 4, 1), new DateOnly(2027, 9, 30)));
            await ctx.SaveChangesAsync();
        });

        var act = () => SendAsync(new AdminAddSeason(clubId, new DateOnly(2027, 5, 1), new DateOnly(2027, 10, 30)));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
