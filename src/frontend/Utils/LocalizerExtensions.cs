using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Utils;

public static class LocalizerExtensions
{
    /// <summary>"1 booking" / "3 bookings": the text of <paramref name="key"/> + "One" for a count of 1, otherwise of <paramref name="key"/>, with the count as {0}.</summary>
    public static string Count(this IStringLocalizer locale, string key, int count)
        => string.Format(locale[count == 1 ? key + "One" : key], count);
}
