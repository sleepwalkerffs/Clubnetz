using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class UpdateSeasonTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateSeason_UpdatesSeasonPeriod()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));

        var newStart = new DateOnly(2025, 5, 1);
        var newEnd = new DateOnly(2025, 12, 31);
        await SendAsync(new UpdateSeason(seasonId, newStart, newEnd));

        var club = await QueryAsync(ctx => ctx.Clubs.Include(c => c.Seasons).SingleAsync());
        var season = club.Seasons.Single(s => s.Id == seasonId);
        season.Period.From.Should().Be(newStart);
        season.Period.To.Should().Be(newEnd);
    }

    [Fact]
    public async Task UpdateSeason_NonExistentSeason_ThrowsInvalidOperationException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        var act = () => SendAsync(new UpdateSeason(999999, new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateSeason_OverlappingSeason_ThrowsPreconditionException()
    {
        await QueryAsync(async ctx => await ctx.RemoveMigrationSeedData());

        await SendAsync(new AddSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)));
        var seasonId = await SendAsync(new AddSeason(new DateOnly(2025, 10, 1), new DateOnly(2025, 12, 31)));

        var act = () => SendAsync(new UpdateSeason(seasonId, new DateOnly(2025, 8, 1), new DateOnly(2025, 12, 31)));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
