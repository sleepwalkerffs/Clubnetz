using System.Globalization;
using System.Resources;

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

    /// <summary>Short date and time, e.g. "Sa 3. Oktober, 18:00". Pass the time in the booking's time zone.</summary>
    public static string DateAndTime(CultureInfo culture, DateTimeOffset local)
        => $"{Date(culture, DateOnly.FromDateTime(local.DateTime))}, {Time(culture, TimeOnly.FromDateTime(local.DateTime))}";

    public static string Date(CultureInfo culture, DateOnly date)
        => $"{date.ToString("ddd", culture).TrimEnd('.')} {date.ToString(culture.DateTimeFormat.MonthDayPattern, culture)}";

    public static string Time(CultureInfo culture, TimeOnly time) => time.ToString("HH:mm", culture);
}
