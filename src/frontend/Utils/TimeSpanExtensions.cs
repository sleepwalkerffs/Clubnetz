namespace Bookennis.Client.Utils;

public static class TimeSpanExtensions
{
    public static TimeSpan Round(this TimeSpan span, TimeSpanRoundingType type, MidpointRounding mode = MidpointRounding.ToEven) =>
        type switch
        {
            TimeSpanRoundingType.QuarterMinute => TimeSpan.FromSeconds(Math.Round(span.TotalSeconds / 15, 0, mode) * 15),
            TimeSpanRoundingType.HalfMinute => TimeSpan.FromSeconds(Math.Round(span.TotalSeconds / 30, 0, mode) * 30),
            TimeSpanRoundingType.Minute => TimeSpan.FromSeconds(Math.Round(span.TotalSeconds / 60, 0, mode) * 60),
            TimeSpanRoundingType.QuarterHour => TimeSpan.FromMinutes(Math.Round(span.TotalMinutes / 15, 0, mode) * 15),
            TimeSpanRoundingType.HalfHour => TimeSpan.FromMinutes(Math.Round(span.TotalMinutes / 30, 0, mode) * 30),
            TimeSpanRoundingType.Hour => TimeSpan.FromMinutes(Math.Round(span.TotalMinutes / 60, 0, mode) * 60),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
}

public enum TimeSpanRoundingType
{
    QuarterMinute,
    HalfMinute,
    Minute,
    QuarterHour,
    HalfHour,
    Hour,
}