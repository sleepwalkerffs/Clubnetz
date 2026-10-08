using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class AddSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddSeason_AddsSeasonToClub()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var startDate = new DateOnly(2025, 4, 1);
        var endDate = new DateOnly(2025, 11, 30);

        var seasonId = await SendAsync(new AddSeason(startDate, endDate));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.Seasons).SingleAsync());
        club.Seasons.Should().Contain(s => s.Id == seasonId);
        var season = club.Seasons.Single(s => s.Id == seasonId);
        season.Period.From.Should().Be(startDate);
        season.Period.To.Should().Be(endDate);
    }

    [Fact]
    public async Task AddSeason_OverlappingSeason_ThrowsPreconditionException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));

        var act = () => SendAsync(new AddSeason(new DateOnly(2025, 6, 1), new DateOnly(2025, 12, 31)));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
