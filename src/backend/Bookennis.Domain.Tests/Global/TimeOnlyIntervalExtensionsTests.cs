using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Global;

public class TimeOnlyIntervalExtensionsTests
{
    [Fact]
    public void ContainsReturnsFalseOnBorder()
    {
        var interval = new TimeOnlyInterval(new TimeOnly(16, 0, 0), new TimeOnly(17, 0, 0));

        var timestamp = new DateTimeOffset(2024, 6, 23, 16, 0, 0, TimeSpan.Zero);

        interval.Contains(timestamp).Should().BeFalse();
    }
}
