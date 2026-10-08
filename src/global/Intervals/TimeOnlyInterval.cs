namespace Bookennis.Global.Intervals;

public record TimeOnlyInterval
{
    public static TimeOnlyInterval Max() => new(TimeOnly.MinValue, TimeOnly.MinValue);
    public static readonly TimeOnly EndOfDay = new(0);

    public TimeOnlyInterval(TimeOnly from, TimeOnly to)
    {
        if (to != EndOfDay && to < from)
            throw new ArgumentException("To value can't be before from value");

        From = from;
        To = to;
    }

    public TimeOnly To { get; private init; }

    public TimeOnly From { get; private init; }
}