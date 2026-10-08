using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class GetCourtBlockingConflictsTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateOnly Day = CourtBlockingSeed.Day;

    [Fact]
    public async Task GetCourtBlockingConflicts_ReturnsUpcomingBookingsInTheBlockedTime()
    {
        var (first, second) = await QueryAsync(async ctx =>
        {
            var first = await CourtBlockingSeed.SeedBooking(ctx, Day, 10);
            var second = await CourtBlockingSeed.SeedBooking(ctx, Day.AddDays(7), 13, TestDataSeed.Court2Id);

            // Not affected: before the blocking, the week in between and a booking that is over
            await CourtBlockingSeed.SeedBooking(ctx, Day, 9);
            await CourtBlockingSeed.SeedBooking(ctx, Day.AddDays(3), 11);
            await CourtBlockingSeed.SeedBooking(ctx, Day.AddDays(-14), 11);

            return (first.Id, second.Id);
        });

        var data = CourtBlockingSeed.Data(Day.AddDays(-14), [TestDataSeed.Court1Id, TestDataSeed.Court2Id]) with
        {
            RecurrenceIntervalWeeks = 1,
            RecurrenceEndDate = Day.AddDays(7),
        };

        var result = await SendAsync(new GetCourtBlockingConflicts(TestDataSeed.ClubId, data));

        result.Bookings.Select(b => b.BookingId).Should().Equal(first, second);
        result.Bookings[0].CourtName.Should().Be("Court 1");
        result.Bookings[0].Interval.Should().Be(CourtBlockingSeed.At(Day, 10, 11));
        result.Bookings[0].Players.Should().Equal("us er");
        result.Bookings[1].CourtName.Should().Be("Court 2");
    }

    [Fact]
    public async Task GetCourtBlockingConflicts_NoBookings_ReturnsNothing()
    {
        var result = await SendAsync(new GetCourtBlockingConflicts(TestDataSeed.ClubId, CourtBlockingSeed.Data()));

        result.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCourtBlockingConflicts_InvalidBlocking_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new GetCourtBlockingConflicts(TestDataSeed.ClubId, CourtBlockingSeed.Data() with { CourtIds = [] }));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(CourtBlocking.ErrorCode.CourtBlockingCourtRequired));
    }
}
