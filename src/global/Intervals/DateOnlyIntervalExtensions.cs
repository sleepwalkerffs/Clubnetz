namespace Bookennis.Global.Intervals;

public static class DateOnlyIntervalExtensions
{
    public static bool Contains(this DateOnlyInterval interval, DateOnly date)
        => interval.From < date && date < interval.To;

    public static bool Contains(this DateOnlyInterval interval, DateTimeOffset date)
        => interval.From < DateOnly.FromDateTime(date.DateTime) && DateOnly.FromDateTime(date.DateTime) < interval.To;

    public static bool Intersects(this DateOnlyInterval section, DateOnlyInterval intersects)
        => section.From < intersects.To && section.To > intersects.From;

    public static bool Overlaps(this DateOnlyInterval section, DateOnlyInterval overlaps)
        => section.From < overlaps.From && section.To > overlaps.To;
}