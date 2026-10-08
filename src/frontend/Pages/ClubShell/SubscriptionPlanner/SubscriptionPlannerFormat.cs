using System.Globalization;

namespace Bookennis.Client.Pages.ClubShell.SubscriptionPlanner;

public static class SubscriptionPlannerFormat
{
    /// <summary>Compact, culture aware range such as "5 Oct – 11 Oct" / "5. Okt. – 11. Okt.".</summary>
    public static string DateRange(DateOnly from, DateOnly to)
        => $"{ShortDate(from)} – {ShortDate(to)}";

    public static string ShortDate(DateOnly date)
        => date.ToString(CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "de" ? "d. MMM" : "d MMM", CultureInfo.CurrentCulture);

    public static string Number(double value)
        => value.ToString("0.#", CultureInfo.CurrentCulture);
}
