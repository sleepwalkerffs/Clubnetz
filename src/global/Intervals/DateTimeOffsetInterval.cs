namespace Bookennis.Global.Intervals;

public record DateTimeOffsetInterval
{
    public static DateTimeOffsetInterval Max() => new(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

    public DateTimeOffsetInterval(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from)
            throw new ArgumentException("To value can't be before from value");

        if (to.Offset != from.Offset)
            throw new ArgumentException("Values don't have the same offset");

        From = from;
        To = to;
    }

    public DateTimeOffset To { get; private init; }

    public DateTimeOffset From { get; private init; }
}