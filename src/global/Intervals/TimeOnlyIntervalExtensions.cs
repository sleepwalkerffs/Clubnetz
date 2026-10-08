using System.Diagnostics.CodeAnalysis;

namespace Bookennis.Global.Intervals;

public static class TimeOnlyIntervalExtensions
{
    public static bool Contains(this TimeOnlyInterval interval, DateTimeOffset dateTimeOffset)
        => interval.From < TimeOnly.FromDateTime(dateTimeOffset.DateTime) && TimeOnly.FromDateTime(dateTimeOffset.DateTime) < interval.To;

    public static bool Contains(this TimeOnlyInterval interval, TimeOnly time)
        => interval.From < time && time < interval.To;

    public static bool Intersects(this TimeOnlyInterval section, TimeOnlyInterval intersects)
        => section.From < intersects.To && section.To > intersects.From;

    public static bool Overlaps(this TimeOnlyInterval section, TimeOnlyInterval overlaps)
        => section.From < overlaps.From && section.To > overlaps.To;

    public static bool Contains(this TimeOnlyInterval interval, TimeOnlyInterval section)
        => interval.Contains(section.From) && interval.Contains(section.To);

    public static TimeSpan GetDifference(this TimeOnlyInterval interval)
        => interval.To == TimeOnlyInterval.EndOfDay ? interval.From == TimeOnlyInterval.EndOfDay ? TimeSpan.FromHours(24) : TimeSpan.FromHours(24) - interval.From.ToTimeSpan() : interval.To - interval.From;

    public static bool TryCreateTimeOnlyInterval(TimeOnly from, TimeOnly to, [NotNullWhen(true)] out TimeOnlyInterval? interval)
    {
        interval = null;
        try
        {
            interval = new TimeOnlyInterval(from, to);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool TryCreateTimeOnlyInterval(TimeSpan from, TimeSpan to, [NotNullWhen(true)] out TimeOnlyInterval? interval)
        => TryCreateTimeOnlyInterval(TimeOnly.FromTimeSpan(from), TimeOnly.FromTimeSpan(to), out interval);
}