using Bookennis.Api.Business.Wrapped;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;

namespace Bookennis.Api.Tests.Business.Wrapped;

public class PublishSeasonWrappedTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private Task<int> AddSeason(DateOnly from, DateOnly to) => QueryAsync(async ctx =>
    {
        var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(from, to));
        ctx.Add(season);
        await ctx.SaveChangesAsync();
        return season.Id;
    });

    private DateTimeOffset? PublishedAt(int seasonId) => Query(ctx => ctx.Seasons.Single(s => s.Id == seasonId).WrappedPublishedAt);

    [Fact]
    public async Task PublishSeasonWrapped_PastSeason_IsReleasedAndRepeatingKeepsTheDate()
    {
        var seasonId = await AddSeason(new DateOnly(Today.Year - 3, 4, 1), new DateOnly(Today.Year - 3, 9, 30));

        await SendAsync(new PublishSeasonWrapped(TestDataSeed.ClubId, seasonId));
        var first = PublishedAt(seasonId);
        first.Should().NotBeNull();

        await SendAsync(new PublishSeasonWrapped(TestDataSeed.ClubId, seasonId));
        PublishedAt(seasonId).Should().Be(first);
    }

    [Fact]
    public async Task PublishSeasonWrapped_RunningSeason_IsAllowed()
    {
        var seasonId = await AddSeason(Today.AddDays(-20), Today.AddDays(20));

        await SendAsync(new PublishSeasonWrapped(TestDataSeed.ClubId, seasonId));

        PublishedAt(seasonId).Should().NotBeNull();
    }

    [Fact]
    public async Task PublishSeasonWrapped_UpcomingSeason_IsRejected()
    {
        var seasonId = await AddSeason(Today.AddDays(1), Today.AddDays(100));

        var act = () => SendAsync(new PublishSeasonWrapped(TestDataSeed.ClubId, seasonId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(Season.ErrorCode.WrappedSeasonNotStarted));
        PublishedAt(seasonId).Should().BeNull();
    }

    [Fact]
    public async Task PublishSeasonWrapped_SeasonOfAnotherClub_IsNotFound()
    {
        var seasonId = await AddSeason(Today.AddDays(-20), Today.AddDays(20));

        var act = () => SendAsync(new PublishSeasonWrapped(TestDataSeed.ClubId + 1, seasonId));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UnpublishSeasonWrapped_TakesTheReleaseBack()
    {
        var seasonId = await AddSeason(new DateOnly(Today.Year - 3, 4, 1), new DateOnly(Today.Year - 3, 9, 30));
        await SendAsync(new PublishSeasonWrapped(TestDataSeed.ClubId, seasonId));

        await SendAsync(new UnpublishSeasonWrapped(TestDataSeed.ClubId, seasonId));

        PublishedAt(seasonId).Should().BeNull();
    }

    [Fact]
    public async Task UnpublishSeasonWrapped_SeasonOfAnotherClub_IsNotFound()
    {
        var seasonId = await AddSeason(new DateOnly(Today.Year - 3, 4, 1), new DateOnly(Today.Year - 3, 9, 30));

        var act = () => SendAsync(new UnpublishSeasonWrapped(TestDataSeed.ClubId + 1, seasonId));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
