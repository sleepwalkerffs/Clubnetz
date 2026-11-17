using Bookennis.Client.Localization;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Utils;

/// <summary>
/// Readable names of a club's region settings (time zone, country, currency) from <see cref="SharedLocale"/>,
/// so the admin panel and the club settings write them the same way.
/// </summary>
public static class RegionNames
{
    /// <summary>"Eastern Time (New York)" for "America/New_York"; the IANA id itself for a zone without a text.</summary>
    public static string TimeZone(IStringLocalizer<SharedLocale> locale, string timeZoneId)
    {
        var text = locale[$"TimeZone_{timeZoneId.Replace('/', '_')}"];
        return text.ResourceNotFound ? timeZoneId : text.Value;
    }

    public static string Country(IStringLocalizer<SharedLocale> locale, Country country)
    {
        var text = locale[country.ToString()];
        return text.ResourceNotFound ? country.ToString() : text.Value;
    }

    /// <summary>"US dollar ($)" for "usd"; the upper-case code for a currency without a text.</summary>
    public static string Currency(IStringLocalizer<SharedLocale> locale, string currency)
    {
        var text = locale[$"Currency_{currency.ToLowerInvariant()}"];
        return text.ResourceNotFound ? currency.ToUpperInvariant() : text.Value;
    }
}
