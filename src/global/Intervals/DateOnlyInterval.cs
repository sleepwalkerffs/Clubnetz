namespace Bookennis.Global.Intervals;

public record DateOnlyInterval
{
    public DateOnlyInterval(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("To value can't be before from value");

        From = from;
        To = to;
    }

    public DateOnly To { get; private init; }

    public DateOnly From { get; private init; }
}