using System.Globalization;
using Bookennis.Client.Localization.Pages.MyClub;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.MyClub.Components;

internal static class MyClubHelpers
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Booking page for the coming week, same link as in the navigation.</summary>
    public static string BookingUrl(int clubId)
        => $"/clubs/{clubId}/booking?Start={Today.ToString(CultureInfo.InvariantCulture)}&End={Today.AddDays(6).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>"Today", "Tomorrow" or "In 3 days" for the start of a booking.</summary>
    public static string Countdown(IStringLocalizer<MyClubLocale> locale, DateTimeOffset start)
    {
        var days = DateOnly.FromDateTime(start.DateTime).DayNumber - Today.DayNumber;
        return days switch
        {
            <= 0 => locale["Today"],
            1 => locale["Tomorrow"],
            _ => string.Format(locale["InDays"], days),
        };
    }
}
