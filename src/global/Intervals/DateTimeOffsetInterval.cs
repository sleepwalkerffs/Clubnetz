namespace Bookennis.Global.Intervals;

public record DateTimeOffsetInterval
{
    public static DateTimeOffsetInterval Max() => new(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

    public DateTimeOffsetInterval(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from)
            throw new ArgumentException("To value can't be before from value");

        // The offsets may differ: the client reads times in local time, so an interval across a daylight saving change
        // starts at +01:00 and ends at +02:00. Comparing and subtracting DateTimeOffset values works on the instant.
        From = from;
        To = to;
    }

    public DateTimeOffset To { get; private init; }

    public DateTimeOffset From { get; private init; }
}