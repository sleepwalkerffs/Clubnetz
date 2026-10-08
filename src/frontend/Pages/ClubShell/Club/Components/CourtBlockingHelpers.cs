using System.Globalization;
using Bookennis.Client.Localization.Pages.CourtBlockings;
using Bookennis.Shared.Controller.Club;
using Bookennis.Shared.Controller.CourtBlockings;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Club.Components;

internal static class CourtBlockingHelpers
{
    /// <summary>Roles that can block courts, same as the backend policy <c>CourtBlockingManager</c>.</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles)
        => roles.Any(r => r is MemberRole.Maintainer or MemberRole.SportsDirector or MemberRole.Admin);

    public static string Url(int clubId) => $"/clubs/{clubId}/court-blockings";

    /// <summary>Whether the blocked time window touches the (local) day.</summary>
    public static bool IsOn(this CourtBlockingOccurrenceDto occurrence, DateOnly day)
    {
        var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue));
        var dayEnd = new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return occurrence.Interval.From < dayEnd && occurrence.Interval.To > dayStart;
    }

    public static bool IsActive(this CourtBlockingOccurrenceDto occurrence, DateTimeOffset now)
        => occurrence.Interval.From <= now && now < occurrence.Interval.To;

    /// <summary>"All courts" or the aliases of the blocked courts in the order of the club.</summary>
    public static string CourtsText(IStringLocalizer<CourtBlockingsLocale> locale, IReadOnlyCollection<int> courtIds, IReadOnlyCollection<CourtResult> courts)
    {
        if (courts.Count > 1 && courts.All(c => courtIds.Contains(c.CourtId)))
            return locale["AllCourts"];

        return string.Join(", ", courts.Where(c => courtIds.Contains(c.CourtId)).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => c.Name));
    }

    /// <summary>E.g. "Sat, 10 Oct 2026 · 14:00 – 18:00", "Sat, 10 Oct 2026 – Sun, 11 Oct 2026 · All day" or "Every Tuesday · 16:00 – 18:00".</summary>
    public static string WhenText(IStringLocalizer<CourtBlockingsLocale> locale, CourtBlockingDto blocking)
    {
        var isMultiDay = blocking.EndDate != blocking.StartDate;
        var start = FormatDate(locale, blocking.StartDate);
        var end = FormatDate(locale, blocking.EndDate);
        var weekday = blocking.StartDate.ToString("dddd", CultureInfo.CurrentCulture);

        var days = blocking.RecurrenceIntervalWeeks switch
        {
            null => isMultiDay ? $"{start} – {end}" : start,
            1 => string.Format(locale["RecurringWeekly"], weekday),
            var weeks => string.Format(locale["RecurringEveryWeeks"], weekday, weeks),
        };

        if (blocking.StartTime is not { } startTime || blocking.EndTime is not { } endTime)
            return $"{days} · {locale["AllDay"]}";

        // The times of a blocking over several days belong to its first and last day
        return isMultiDay && blocking.RecurrenceIntervalWeeks is null
            ? $"{start}, {startTime:hh\\:mm} – {end}, {endTime:hh\\:mm}"
            : $"{days} · {startTime:hh\\:mm} – {endTime:hh\\:mm}";
    }

    /// <summary>E.g. "until Tue, 3 Nov 2026 · 12 dates"; <c>null</c> for a single blocking.</summary>
    public static string? RecurrenceText(IStringLocalizer<CourtBlockingsLocale> locale, CourtBlockingDto blocking)
        => blocking is { RecurrenceIntervalWeeks: not null, RecurrenceEndDate: { } until }
            ? string.Format(locale["RecurringUntil"], FormatDate(locale, until), blocking.OccurrenceCount)
            : null;

    public static string FormatDate(IStringLocalizer<CourtBlockingsLocale> locale, DateOnly date)
        => date.ToString(locale["DateFormat"], CultureInfo.CurrentCulture);

    /// <summary>"Active now", "Today", "Tomorrow", "In 3 days" or "Over".</summary>
    public static string StatusText(IStringLocalizer<CourtBlockingsLocale> locale, CourtBlockingDto blocking, DateTimeOffset now)
    {
        if (blocking.NextOccurrence is not { } next)
            return locale["StatusOver"];

        if (next <= now)
            return locale["StatusNow"];

        return (DateOnly.FromDateTime(next.LocalDateTime).DayNumber - DateOnly.FromDateTime(now.LocalDateTime).DayNumber) switch
        {
            0 => locale["Today"],
            1 => locale["Tomorrow"],
            var days => string.Format(locale["InDays"], days),
        };
    }
}
