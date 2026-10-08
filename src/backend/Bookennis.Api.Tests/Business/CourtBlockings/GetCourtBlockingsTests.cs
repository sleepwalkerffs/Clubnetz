using Bookennis.Api.Business.CourtBlockings;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class GetCourtBlockingsTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task GetCourtBlockings_ReturnsUpcomingBlockingsOrderedByNextOccurrence()
    {
        await QueryAsync(async ctx =>
        {
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Today.AddDays(20)) with { Title = "Later" });
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Today.AddDays(-5)) with { Title = "Over" });

            // Started in the past, the next occurrence is in two days
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Today.AddDays(-5), [TestDataSeed.Court1Id, TestDataSeed.Court2Id]) with
            {
                Title = "Youth training",
                RecurrenceIntervalWeeks = 1,
                RecurrenceEndDate = Today.AddDays(30),
            });
        });

        var result = await SendAsync(new GetCourtBlockings(TestDataSeed.ClubId, IncludePast: false));

        result.Blockings.Select(b => b.Title).Should().Equal("Youth training", "Later");

        var training = result.Blockings[0];
        training.CourtIds.Should().BeEquivalentTo([TestDataSeed.Court1Id, TestDataSeed.Court2Id]);
        training.StartDate.Should().Be(Today.AddDays(-5));
        training.StartTime.Should().Be(TimeSpan.FromHours(10));
        training.EndTime.Should().Be(TimeSpan.FromHours(14));
        training.RecurrenceIntervalWeeks.Should().Be(1);
        training.RecurrenceEndDate.Should().Be(Today.AddDays(30));
        training.OccurrenceCount.Should().Be(6);
        training.NextOccurrence.Should().Be(CourtBlockingSeed.At(Today.AddDays(2), 10, 14).From);
    }

    [Fact]
    public async Task GetCourtBlockings_IncludePast_AppendsBlockingsThatAreOver()
    {
        await QueryAsync(async ctx =>
        {
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Today.AddDays(-20)) with { Title = "Long ago" });
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Today.AddDays(-5)) with { Title = "Recently" });
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Today.AddDays(20)) with { Title = "Later" });
        });

        var result = await SendAsync(new GetCourtBlockings(TestDataSeed.ClubId, IncludePast: true));

        result.Blockings.Select(b => b.Title).Should().Equal("Later", "Recently", "Long ago");
        result.Blockings[1].NextOccurrence.Should().BeNull();
    }

    [Fact]
    public async Task GetCourtBlockings_OtherClub_ReturnsNothing()
    {
        await QueryAsync(ctx => CourtBlockingSeed.Seed(ctx));

        var result = await SendAsync(new GetCourtBlockings(TestDataSeed.ClubId + 1, IncludePast: true));

        result.Blockings.Should().BeEmpty();
    }
}
