using System.Globalization;
using System.Resources;
using Bookennis.Shared.Utils;

namespace Bookennis.Api.Business.Notifications;

/// <summary>
/// Localized text fragments that are passed to the notification emails as variables
/// (Resources/Localization/Notifications(.de).resx). The emails themselves are club email templates.
/// </summary>
public static class NotificationTexts
{
    private static readonly ResourceManager Texts = new("Bookennis.Api.Resources.Localization.Notifications", typeof(NotificationTexts).Assembly);

    public static string Get(CultureInfo culture, string key, params object?[] args)
    {
        var text = Texts.GetString(key, culture) ?? key;
        return args.Length == 0 ? text : string.Format(culture, text, args);
    }

    /// <summary>Long date, e.g. "Samstag, 3. Oktober 2026" or "Saturday, October 3, 2026" (pass the club's <see cref="RegionalCulture"/>).</summary>
    public static string Date(CultureInfo culture, DateOnly date) => date.ToString("D", culture);

    /// <summary>A time of the club's clock: "18:00", "6:00 PM" in the United States (pass the club's <see cref="RegionalCulture"/>).</summary>
    public static string Time(CultureInfo culture, TimeOnly time) => RegionalCulture.Of(culture).Time(time);
}
