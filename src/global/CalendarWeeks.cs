using System.Globalization;

namespace Bookennis.Global;

/// <summary>An ISO 8601 calendar week, identified by its Monday.</summary>
public readonly record struct CalendarWeek(DateOnly Monday, int WeekNumber, int Year)
{
    public DateOnly Sunday => Monday.AddDays(6);
}

public static class CalendarWeeks
{
    public static DateOnly GetMonday(DateOnly date)
        => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static CalendarWeek FromDate(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return new CalendarWeek(GetMonday(date), ISOWeek.GetWeekOfYear(dateTime), ISOWeek.GetYear(dateTime));
    }

    /// <summary>All ISO weeks that overlap the inclusive range [<paramref name="start"/>, <paramref name="end"/>].</summary>
    public static IReadOnlyList<CalendarWeek> Between(DateOnly start, DateOnly end)
    {
        var weeks = new List<CalendarWeek>();
        if (end < start)
            return weeks;

        for (var monday = GetMonday(start); monday <= end; monday = monday.AddDays(7))
            weeks.Add(FromDate(monday));

        return weeks;
    }
}
