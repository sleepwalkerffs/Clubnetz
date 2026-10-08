using Bookennis.Api.Business.CourtBlockings;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class DeleteCourtBlockingTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteCourtBlocking_RemovesBlockingWithCourtsAndOccurrences()
    {
        var id = await QueryAsync(async ctx => (await CourtBlockingSeed.Seed(ctx)).Id);

        await SendAsync(new DeleteCourtBlocking(TestDataSeed.ClubId, id));

        var (blockings, courts, occurrences) = await QueryAsync(async ctx => (
            await ctx.CourtBlockings.CountAsync(),
            await ctx.CourtBlockingCourts.CountAsync(),
            await ctx.CourtBlockingOccurrences.CountAsync()));

        blockings.Should().Be(0);
        courts.Should().Be(0);
        occurrences.Should().Be(0);
    }

    [Fact]
    public async Task DeleteCourtBlocking_BlockingOfAnotherClub_ThrowsEntityNotFoundException()
    {
        var id = await QueryAsync(async ctx => (await CourtBlockingSeed.Seed(ctx)).Id);

        var act = () => SendAsync(new DeleteCourtBlocking(TestDataSeed.ClubId + 1, id));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
