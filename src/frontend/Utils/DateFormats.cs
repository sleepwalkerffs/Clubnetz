using Bookennis.Shared.Utils;

namespace Bookennis.Client.Utils;

public static class DateFormats
{
    /// <summary>
    /// A day pattern written the German way ("ddd, d. MMM yyyy") in the way of the club's region (<see cref="RegionalCulture.Current"/>):
    /// German keeps the dot after the day, English in Europe drops it ("Wed, 4 Nov 2026"), English in the United States
    /// puts the month first ("Wed, Nov 4, 2026"). Never hardcode such a pattern elsewhere.
    /// </summary>
    public static string Localized(string germanPattern)
    {
        var region = RegionalCulture.Current;
        if (region.IsGerman)
            return germanPattern;

        if (!region.IsUnitedStates)
            return germanPattern.Replace("d. ", "d ", StringComparison.Ordinal);

        return germanPattern
            .Replace("d. MMMM yyyy", "MMMM d, yyyy", StringComparison.Ordinal)
            .Replace("d. MMM yyyy", "MMM d, yyyy", StringComparison.Ordinal)
            .Replace("d. MMMM", "MMMM d", StringComparison.Ordinal)
            .Replace("d. MMM", "MMM d", StringComparison.Ordinal);
    }
}
