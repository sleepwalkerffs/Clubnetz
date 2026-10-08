using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.CourtBlockings;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

public class CourtBlockingQueryExtensionsTests
{
    private static readonly SaveCourtBlockingModel Model = new()
    {
        Title = "Maintenance",
        CourtIds = [1, 2],
        StartDate = new DateOnly(2026, 10, 10),
        EndDate = new DateOnly(2026, 10, 11),
        StartTime = new TimeSpan(14, 30, 0),
        EndTime = new TimeSpan(18, 0, 0),
        TimeZoneInfoId = "Europe/Vienna",
        RecurrenceIntervalWeeks = 2,
        RecurrenceEndDate = new DateOnly(2026, 11, 7),
    };

    [Fact]
    public void ToBlockingData_MapsTheModel()
    {
        var data = Model.ToBlockingData();

        data.Should().BeEquivalentTo(new CourtBlocking.BlockingData(
            "Maintenance",
            [1, 2],
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 11),
            new TimeOnly(14, 30),
            new TimeOnly(18, 0),
            "Europe/Vienna",
            2,
            new DateOnly(2026, 11, 7)));
    }

    [Fact]
    public void ToBlockingData_AllDay_HasNoTimes()
    {
        var data = (Model with { StartTime = null, EndTime = null }).ToBlockingData();

        data.StartTime.Should().BeNull();
        data.EndTime.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void ToBlockingData_TimeIsNoTimeOfDay_ThrowsPreconditionException(int hours)
    {
        var act = () => (Model with { EndTime = TimeSpan.FromHours(hours) }).ToBlockingData();

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtBlocking.ErrorCode.CourtBlockingInvalidTimeRange));
    }
}
