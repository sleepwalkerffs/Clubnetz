using System.Globalization;

namespace Bookennis.Client.Utils;

public static class DateFormats
{
    /// <summary>
    /// A day pattern written the German way ("ddd, d. MMM") for the current culture: German keeps the dot after the day,
    /// every other language gets "ddd, d MMM" instead of "Wed, 4. Nov".
    /// </summary>
    public static string Localized(string germanPattern)
        => CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "de" ? germanPattern : germanPattern.Replace("d. ", "d ");
}
