using System.Globalization;
using Bookennis.Client.Localization.Pages.Calendar;
using Bookennis.Shared.Controller.ClubEvents;
using Bookennis.Shared.Controller.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Calendar.Components;

internal static class ClubEventHelpers
{
    public static readonly ClubEventCategory[] Categories =
        [ClubEventCategory.WorkEffort, ClubEventCategory.Social, ClubEventCategory.Tournament, ClubEventCategory.Meeting, ClubEventCategory.Other];

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Roles that can plan events, same as the backend policy <c>ClubEventManager</c>.</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles)
        => roles.Any(r => r is MemberRole.Maintainer or MemberRole.SportsDirector or MemberRole.YouthSportsDirector or MemberRole.Admin);

    public static string CalendarUrl(int clubId) => $"/clubs/{clubId}/calendar";

    public static string EventUrl(int clubId, int clubEventId) => $"/clubs/{clubId}/calendar/{clubEventId}";

    /// <summary>Absolute link of an event, e.g. to share it via WhatsApp.</summary>
    public static string AbsoluteEventUrl(NavigationManager navigationManager, int clubId, int clubEventId)
        => navigationManager.BaseUri.TrimEnd('/') + EventUrl(clubId, clubEventId);

    public static string Emoji(ClubEventCategory category) => category switch
    {
        ClubEventCategory.WorkEffort => "🛠️",
        ClubEventCategory.Social => "🎉",
        ClubEventCategory.Tournament => "🏆",
        ClubEventCategory.Meeting => "🗣️",
        _ => "📌",
    };

    /// <summary>Accent color of a category. Uses the dark mode tokens, so the color stays readable on dark surfaces.</summary>
    public static string Color(ClubEventCategory category) => category switch
    {
        ClubEventCategory.WorkEffort => "var(--bk-dark-amber, #D97706)",
        ClubEventCategory.Social => "var(--bk-dark-pink, #DB2777)",
        ClubEventCategory.Tournament => "var(--bk-dark-indigo, #4F46E5)",
        ClubEventCategory.Meeting => "var(--bk-dark-blue, #0284C7)",
        _ => "var(--bk-dark-green, #059669)",
    };

    /// <summary>Formats a count with the "{key}One" text for exactly one, e.g. "1 event" vs. "3 events".</summary>
    public static string Count(IStringLocalizer<CalendarLocale> locale, string key, int count)
        => string.Format(locale[count == 1 ? key + "One" : key], count);

    public static string CategoryName(IStringLocalizer<CalendarLocale> locale, ClubEventCategory category) => locale[$"Category_{category}"];

    /// <summary>"Today", "Tomorrow", "In 3 days", or "Now" for running multi-day events.</summary>
    public static string Countdown(IStringLocalizer<CalendarLocale> locale, DateOnly start, DateOnly end)
    {
        var days = start.DayNumber - Today.DayNumber;
        if (days < 0)
            return end >= Today ? locale["Now"] : locale["Past"];

        return days switch
        {
            0 => locale["Today"],
            1 => locale["Tomorrow"],
            _ => string.Format(locale["InDays"], days),
        };
    }

    /// <summary>E.g. "Sat, 10 Oct" or "Sat, 10 Oct – Mon, 12 Oct" (the format is localized).</summary>
    public static string FormatDateRange(IStringLocalizer<CalendarLocale> locale, DateOnly start, DateOnly? end, bool withYear = false)
    {
        var format = locale[withYear ? "DateFormatWithYear" : "DateFormat"];
        var text = start.ToString(format, CultureInfo.CurrentCulture);
        return end is { } last && last != start ? $"{text} – {last.ToString(format, CultureInfo.CurrentCulture)}" : text;
    }

    /// <summary>E.g. "09:00 – 13:00", "from 09:00" or "All day".</summary>
    public static string FormatTime(IStringLocalizer<CalendarLocale> locale, TimeOnly? start, TimeOnly? end)
    {
        if (start is null)
            return locale["AllDay"];

        return end is null
            ? string.Format(locale["FromTime"], start.Value.ToString("HH:mm"))
            : $"{start.Value:HH:mm} – {end.Value:HH:mm}";
    }

    public static string FormatDeadline(IStringLocalizer<CalendarLocale> locale, DateTimeOffset deadline)
        => deadline.ToLocalTime().ToString(locale["DateFormat"] + ", HH:mm", CultureInfo.CurrentCulture);

    /// <summary>
    /// Plain text to share: title, date, time, location and the link.
    /// Contains no emojis, because WhatsApp breaks them when they are passed via a wa.me link.
    /// <paramref name="forWhatsApp"/> marks the title bold using WhatsApp's *bold* syntax.
    /// </summary>
    public static string ShareText(IStringLocalizer<CalendarLocale> locale, ClubEventDto clubEvent, string url, bool forWhatsApp = false)
    {
        var lines = new List<string>
        {
            forWhatsApp ? $"*{clubEvent.Title.Trim()}*" : clubEvent.Title,
            $"{FormatDateRange(locale, clubEvent.StartDate, clubEvent.EndDate, withYear: true)} · {FormatTime(locale, clubEvent.StartTime, clubEvent.EndTime)}",
        };

        if (!string.IsNullOrWhiteSpace(clubEvent.Location))
            lines.Add(clubEvent.Location);

        if (clubEvent.RegistrationEnabled)
            lines.Add(locale["ShareRegisterHint"]);

        lines.Add(url);
        return string.Join('\n', lines);
    }
}
