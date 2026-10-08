using Bookennis.Global;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.SubscriptionPlans;

public class CalendarWeeksTests
{
    [Fact]
    public void Between_CrossesYearBoundary_UsesIsoWeekNumbers()
    {
        var weeks = CalendarWeeks.Between(new DateOnly(2026, 12, 24), new DateOnly(2027, 1, 6));

        weeks.Select(w => (w.WeekNumber, w.Year)).Should().Equal((52, 2026), (53, 2026), (1, 2027));
        weeks[0].Monday.Should().Be(new DateOnly(2026, 12, 21));
        weeks[^1].Sunday.Should().Be(new DateOnly(2027, 1, 10));
    }

    [Fact]
    public void Between_SingleDay_ReturnsItsWeek()
    {
        var weeks = CalendarWeeks.Between(new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 27));

        weeks.Should().ContainSingle().Which.Should().Be(new CalendarWeek(new DateOnly(2026, 9, 21), 39, 2026));
    }

    [Fact]
    public void Between_EndBeforeStart_ReturnsEmpty()
        => CalendarWeeks.Between(new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 1)).Should().BeEmpty();
}
