using System.Globalization;
using System.Resources;
using Bookennis.Shared.Utils;

namespace Bookennis.Api.Business.Push;

/// <summary>Localized texts of the push notifications (Resources/Localization/Push(.de).resx).</summary>
public static class PushTexts
{
    private static readonly ResourceManager Texts = new("Bookennis.Api.Resources.Localization.Push", typeof(PushTexts).Assembly);

    public static string Get(CultureInfo culture, string key, params object?[] args)
    {
        var text = Texts.GetString(key, culture) ?? key;
        return args.Length == 0 ? text : string.Format(culture, text, args);
    }

    /// <summary>Short date and time, e.g. "Sa 3. Oktober, 18:00" or "Sat October 3, 6:00 PM". Pass the time in the club's time zone and the club's culture.</summary>
    public static string DateAndTime(CultureInfo culture, DateTimeOffset local)
        => $"{Date(culture, DateOnly.FromDateTime(local.DateTime))}, {Time(culture, TimeOnly.FromDateTime(local.DateTime))}";

    public static string Date(CultureInfo culture, DateOnly date)
        => $"{date.ToString("ddd", culture).TrimEnd('.')} {date.ToString(culture.DateTimeFormat.MonthDayPattern, culture)}";

    /// <summary>A time of the club's clock: "18:00", "6:00 PM" in the United States (pass the club's <see cref="RegionalCulture"/>).</summary>
    public static string Time(CultureInfo culture, TimeOnly time) => RegionalCulture.Of(culture).Time(time);
}
