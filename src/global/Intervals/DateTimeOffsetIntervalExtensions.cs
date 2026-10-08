using System.Diagnostics.CodeAnalysis;
using EntityFrameworkCore.Projectables;

namespace Bookennis.Global.Intervals;

public static class DateTimeOffsetIntervalExtensions
{
    [Projectable]
    public static bool Contains(this DateTimeOffsetInterval offsetInterval, DateTimeOffset dateTimeOffset) => offsetInterval.From < dateTimeOffset && dateTimeOffset < offsetInterval.To;

    [Projectable]
    public static bool Intersects(this DateTimeOffsetInterval section, DateTimeOffsetInterval intersects) => section.From < intersects.To && section.To > intersects.From;

    [Projectable]
    public static bool Overlaps(this DateTimeOffsetInterval section, DateTimeOffsetInterval overlaps) => section.From < overlaps.From && section.To > overlaps.To;

    public static bool TryToTimeOnly(this DateTimeOffsetInterval interval, [NotNullWhen(true)] out TimeOnlyInterval? timeOnlyInterval) =>
        TimeOnlyIntervalExtensions.TryCreateTimeOnlyInterval(TimeOnly.FromDateTime(interval.From.DateTime), TimeOnly.FromDateTime(interval.To.DateTime), out timeOnlyInterval);
}
