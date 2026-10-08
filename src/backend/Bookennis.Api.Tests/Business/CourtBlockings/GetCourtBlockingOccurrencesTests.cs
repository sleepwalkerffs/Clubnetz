using Bookennis.Api.Business.CourtBlockings;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class GetCourtBlockingOccurrencesTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateOnly Day = CourtBlockingSeed.Day;

    [Fact]
    public async Task GetCourtBlockingOccurrences_ReturnsTheOccurrencesOfTheRange()
    {
        var (recurringId, singleId) = await QueryAsync(async ctx =>
        {
            var recurring = await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Day.AddDays(-14), [TestDataSeed.Court1Id, TestDataSeed.Court2Id]) with
            {
                Title = "Youth training",
                RecurrenceIntervalWeeks = 1,
                RecurrenceEndDate = Day.AddDays(60),
            });
            var single = await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Day.AddDays(3)) with { StartTime = null, EndTime = null });
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(Day.AddDays(30)) with { Title = "Outside the range" });

            return (recurring.Id, single.Id);
        });

        var result = await SendAsync(new GetCourtBlockingOccurrences(TestDataSeed.ClubId, Day, Day.AddDays(6)));

        result.Occurrences.Select(o => (o.CourtBlockingId, o.Interval)).Should().Equal(
            (recurringId, CourtBlockingSeed.At(Day, 10, 14)),
            (singleId, CourtBlockingSeed.AllDay(Day.AddDays(3))),
            (recurringId, CourtBlockingSeed.At(Day.AddDays(7), 10, 14)));

        var training = result.Occurrences[0];
        training.Title.Should().Be("Youth training");
        training.IsRecurring.Should().BeTrue();
        training.CourtIds.Should().BeEquivalentTo([TestDataSeed.Court1Id, TestDataSeed.Court2Id]);

        result.Occurrences[1].IsRecurring.Should().BeFalse();
        result.Occurrences[1].CourtIds.Should().Equal(TestDataSeed.Court1Id);
    }

    [Fact]
    public async Task GetCourtBlockingOccurrences_OtherClub_ReturnsNothing()
    {
        await QueryAsync(ctx => CourtBlockingSeed.Seed(ctx));

        var result = await SendAsync(new GetCourtBlockingOccurrences(TestDataSeed.ClubId + 1, Day, Day));

        result.Occurrences.Should().BeEmpty();
    }
}
