using Bookennis.Domain.Courts;
using Bookennis.Domain.Courts.Events;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Courts;

public class CourtBlockingTests
{
    private const string Vienna = "Europe/Vienna";

    // A Saturday in summer time (UTC+2)
    private static readonly DateOnly Day = new(2026, 10, 10);

    [Fact]
    public void Constructor_TimedBlocking_CreatesOneOccurrenceInUtc()
    {
        var blocking = new CourtBlocking(1, Data());

        blocking.Title.Should().Be("Club championship");
        blocking.IsAllDay.Should().BeFalse();
        blocking.IsRecurring.Should().BeFalse();
        blocking.Courts.Select(c => c.CourtId).Should().Equal(1, 2);
        Intervals(blocking).Should().Equal((Utc(10, 10, 12), Utc(10, 10, 16)));
        blocking.Events.OfType<CourtBlockedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Constructor_MultiDayBlocking_LastsFromStartTimeToEndTime()
    {
        var blocking = new CourtBlocking(1, Data() with { EndDate = Day.AddDays(1), StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(9, 0) });

        Intervals(blocking).Should().Equal((Utc(10, 10, 12), Utc(10, 11, 7)));
    }

    [Fact]
    public void Constructor_AllDayBlocking_CoversTheLocalDays()
    {
        var blocking = new CourtBlocking(1, Data() with { EndDate = Day.AddDays(1), StartTime = null, EndTime = null });

        blocking.IsAllDay.Should().BeTrue();
        Intervals(blocking).Should().Equal((Utc(10, 9, 22), Utc(10, 11, 22)));
    }

    [Fact]
    public void Constructor_RecurringBlocking_KeepsTheLocalTimeAcrossDaylightSavingTime()
    {
        // Summer time ends on 25 October 2026
        var tuesday = new DateOnly(2026, 10, 20);
        var blocking = new CourtBlocking(1, Data() with
        {
            StartDate = tuesday,
            EndDate = tuesday,
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(18, 0),
            RecurrenceIntervalWeeks = 1,
            RecurrenceEndDate = new DateOnly(2026, 11, 3),
        });

        blocking.IsRecurring.Should().BeTrue();
        Intervals(blocking).Should().Equal(
            (Utc(10, 20, 14), Utc(10, 20, 16)),
            (Utc(10, 27, 15), Utc(10, 27, 17)),
            (Utc(11, 3, 15), Utc(11, 3, 17)));
    }

    [Fact]
    public void Constructor_RecurringEveryTwoWeeks_SkipsTheWeeksInBetween()
    {
        var blocking = new CourtBlocking(1, Data() with { RecurrenceIntervalWeeks = 2, RecurrenceEndDate = Day.AddDays(27) });

        blocking.Occurrences.Should().HaveCount(2);
        blocking.Occurrences[1].Interval.From.Should().Be(Utc(10, 24, 12));
    }

    [Fact]
    public void Constructor_WithoutRecurrence_IgnoresTheRecurrenceEndDate()
    {
        var blocking = new CourtBlocking(1, Data() with { RecurrenceEndDate = Day.AddDays(30) });

        blocking.RecurrenceEndDate.Should().BeNull();
        blocking.Occurrences.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Constructor_EmptyTitle_Throws(string? title)
    {
        var act = () => new CourtBlocking(1, Data() with { Title = title });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingTitleRequired);
    }

    [Fact]
    public void Constructor_TitleTooLong_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { Title = new string('x', CourtBlocking.MaxTitleLength + 1) });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingTitleTooLong);
    }

    [Fact]
    public void Constructor_NoCourts_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { CourtIds = [] });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingCourtRequired);
    }

    [Fact]
    public void Constructor_EndDateBeforeStartDate_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { EndDate = Day.AddDays(-1) });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidDateRange);
    }

    [Fact]
    public void Constructor_LongerThanAYear_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { EndDate = Day.AddDays(CourtBlocking.MaxDurationInDays) });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidDateRange);
    }

    [Fact]
    public void Constructor_EndTimeNotAfterStartTimeOnSameDay_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(14, 0) });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidTimeRange);
    }

    [Fact]
    public void Constructor_OnlyStartTime_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { EndTime = null });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidTimeRange);
    }

    [Fact]
    public void Constructor_UnknownTimeZone_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { TimeZoneInfoId = "Mars/Olympus" });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidTimeZone);
    }

    [Fact]
    public void Constructor_RecurrenceWithoutEndDate_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { RecurrenceIntervalWeeks = 1 });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidRecurrence);
    }

    [Fact]
    public void Constructor_BlockingLongerThanTheRecurrenceInterval_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { EndDate = Day.AddDays(7), RecurrenceIntervalWeeks = 1, RecurrenceEndDate = Day.AddDays(30) });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingInvalidRecurrence);
    }

    [Fact]
    public void Constructor_TooManyOccurrences_Throws()
    {
        var act = () => new CourtBlocking(1, Data() with { RecurrenceIntervalWeeks = 1, RecurrenceEndDate = Day.AddDays(7 * CourtBlocking.MaxOccurrences) });

        ShouldThrow(act, CourtBlocking.ErrorCode.CourtBlockingTooManyOccurrences);
    }

    [Fact]
    public void Update_OnlyTitle_KeepsOccurrencesAndRaisesNoEvent()
    {
        var blocking = new CourtBlocking(1, Data());
        var occurrence = blocking.Occurrences.Single();
        blocking.Events.Clear();

        blocking.Update(Data() with { Title = " Maintenance " });

        blocking.Title.Should().Be("Maintenance");
        blocking.Occurrences.Single().Should().BeSameAs(occurrence);
        blocking.Events.Should().BeEmpty();
    }

    [Fact]
    public void Update_RemovedCourt_RaisesNoEvent()
    {
        var blocking = new CourtBlocking(1, Data());
        blocking.Events.Clear();

        blocking.Update(Data() with { CourtIds = [2] });

        blocking.Courts.Select(c => c.CourtId).Should().Equal(2);
        blocking.Events.Should().BeEmpty();
    }

    [Fact]
    public void Update_AddedCourt_RaisesEvent()
    {
        var blocking = new CourtBlocking(1, Data());
        blocking.Events.Clear();

        blocking.Update(Data() with { CourtIds = [1, 2, 3] });

        blocking.Courts.Select(c => c.CourtId).Should().Equal(1, 2, 3);
        blocking.Events.OfType<CourtBlockedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Update_ChangedTime_ReplacesOccurrencesAndRaisesEvent()
    {
        var blocking = new CourtBlocking(1, Data());
        blocking.Events.Clear();

        blocking.Update(Data() with { EndTime = new TimeOnly(20, 0) });

        Intervals(blocking).Should().Equal((Utc(10, 10, 12), Utc(10, 10, 18)));
        blocking.Events.OfType<CourtBlockedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Blocks_OnlyBlockedCourtsDuringTheBlockedTime()
    {
        var blocking = new CourtBlocking(1, Data());

        blocking.Blocks(1, new DateTimeOffsetInterval(Utc(10, 10, 15), Utc(10, 10, 17))).Should().BeTrue();
        blocking.Blocks(3, new DateTimeOffsetInterval(Utc(10, 10, 15), Utc(10, 10, 17))).Should().BeFalse();

        // Adjacent bookings are not affected
        blocking.Blocks(1, new DateTimeOffsetInterval(Utc(10, 10, 16), Utc(10, 10, 17))).Should().BeFalse();
        blocking.Blocks(1, new DateTimeOffsetInterval(Utc(10, 10, 11), Utc(10, 10, 12))).Should().BeFalse();
    }

    private static CourtBlocking.BlockingData Data() => new(
        "Club championship",
        [1, 2],
        Day,
        Day,
        new TimeOnly(14, 0),
        new TimeOnly(18, 0),
        Vienna,
        null,
        null);

    private static DateTimeOffset Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static List<(DateTimeOffset From, DateTimeOffset To)> Intervals(CourtBlocking blocking)
        => blocking.Occurrences.Select(o => (o.Interval.From, o.Interval.To)).ToList();

    private static void ShouldThrow(Func<CourtBlocking> act, CourtBlocking.ErrorCode errorCode)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(errorCode.ToString());
}
