using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminClubSeasonsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminClubSeasons_NoSeasons_ReturnsEmptyList()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var result = await SendAsync(new GetAdminClubSeasons(clubId));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAdminClubSeasons_WithSeason_ReturnsSeason()
    {
        var clubId = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            club.AddSeason(new DateOnlyInterval(new DateOnly(2030, 4, 1), new DateOnly(2030, 9, 30)));
            await ctx.SaveChangesAsync();
            return club.Id;
        });

        var result = await SendAsync(new GetAdminClubSeasons(clubId));

        result.Should().HaveCount(1);
        result[0].StartDate.Should().Be(new DateOnly(2030, 4, 1));
        result[0].EndDate.Should().Be(new DateOnly(2030, 9, 30));
    }
}
