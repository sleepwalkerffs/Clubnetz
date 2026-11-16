using System.Globalization;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Utils;

/// <summary>
/// How a club shows dates, times and amounts: the language of the reader (German or English) with the conventions of
/// the club's country. A club in the United States gets the 12-hour clock, month before day, weeks starting on Sunday and
/// its currency; the clubs in Austria, Germany and Switzerland keep the 24-hour clock and day before month.
/// Both the server (emails, push, PDFs) and the app use it, so they always write a date the same way.
/// </summary>
public sealed class RegionalCulture : CultureInfo
{
    private RegionalCulture(string baseName, bool german, Country country, string currency)
        : base(baseName, useUserOverride: false)
    {
        IsGerman = german;
        Country = country;
        Currency = currency.ToLowerInvariant();

        var dateFormat = (DateTimeFormatInfo)DateTimeFormat.Clone();
        var numberFormat = (NumberFormatInfo)NumberFormat.Clone();

        if (IsUnitedStates)
        {
            dateFormat.ShortTimePattern = "h:mm tt";
            dateFormat.LongTimePattern = "h:mm:ss tt";
            dateFormat.ShortDatePattern = "M/d/yyyy";
            // "/" in a pattern is the date separator of the culture: a German reader (de-AT base) would get "11.16.2026"
            dateFormat.DateSeparator = "/";
            dateFormat.MonthDayPattern = german ? "d. MMMM" : "MMMM d";
            dateFormat.AMDesignator = "AM";
            dateFormat.PMDesignator = "PM";
            dateFormat.FirstDayOfWeek = DayOfWeek.Sunday;
        }
        else
        {
            dateFormat.ShortTimePattern = "HH:mm";
            dateFormat.LongTimePattern = "HH:mm:ss";
            dateFormat.ShortDatePattern = "dd.MM.yyyy";
            dateFormat.FirstDayOfWeek = DayOfWeek.Monday;
        }

        numberFormat.CurrencySymbol = CurrencySymbol(Currency);
        if (IsUnitedStates && !german)
        {
            numberFormat.CurrencyPositivePattern = 0; // $1.00
            numberFormat.CurrencyNegativePattern = 1; // -$1.00
        }
        else
        {
            numberFormat.CurrencyPositivePattern = 2; // € 1,00
            numberFormat.CurrencyNegativePattern = 12; // € -1,00
        }

        numberFormat.CurrencyDecimalDigits = 2;
        DateTimeFormat = dateFormat;
        NumberFormat = numberFormat;
    }

    public const string DefaultCurrency = "eur";

    /// <summary>The currencies a club can charge in (ISO 4217, lower case as Stripe writes them).</summary>
    public static readonly IReadOnlyList<string> SupportedCurrencies = ["eur", "usd", "chf"];

    public bool IsGerman { get; }
    public Country Country { get; }

    /// <summary>ISO 4217 code in lower case, e.g. "eur" or "usd".</summary>
    public string Currency { get; }

    public bool IsUnitedStates => Country == Country.UnitedStates;
    public bool UsesTwelveHourClock => IsUnitedStates;

    public static RegionalCulture For(Language language, Country country, string? currency = null)
        => For(language == Language.German, country, currency);

    public static RegionalCulture For(bool german, Country country, string? currency = null)
    {
        var baseName = (german, country) switch
        {
            (true, Country.Germany) => "de-DE",
            (true, Country.Switzerland) => "de-CH",
            (true, _) => "de-AT",
            (false, Country.UnitedStates) => "en-US",
            (false, Country.Germany) => "en-DE",
            (false, Country.Switzerland) => "en-CH",
            _ => "en-AT",
        };

        try
        {
            return new RegionalCulture(baseName, german, country, currency ?? DefaultCurrencyOf(country));
        }
        catch (CultureNotFoundException)
        {
            // The browser may not ship the data of every regional culture: the neutral language has everything that is needed
            return new RegionalCulture(german ? "de" : "en", german, country, currency ?? DefaultCurrencyOf(country));
        }
    }

    /// <summary>The <see cref="RegionalCulture"/> of the current thread, or one made from the current culture for code outside a club.</summary>
    public static RegionalCulture Current
        => CultureInfo.CurrentCulture as RegionalCulture ?? Of(CultureInfo.CurrentCulture);

    /// <summary><paramref name="culture"/> as a <see cref="RegionalCulture"/>: a plain culture gets the conventions of an Austrian club.</summary>
    public static RegionalCulture Of(IFormatProvider? culture)
        => culture switch
        {
            RegionalCulture regional => regional,
            CultureInfo plain => For(plain.TwoLetterISOLanguageName == "de", Country.Austria),
            _ => For(CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "de", Country.Austria),
        };

    public static Country CountryOfTimeZone(string timeZoneId)
        => timeZoneId.StartsWith("America/", StringComparison.Ordinal) ? Country.UnitedStates : Country.Austria;

    public static string DefaultCurrencyOf(Country country) => country switch
    {
        Country.UnitedStates => "usd",
        Country.Switzerland => "chf",
        _ => DefaultCurrency,
    };

    public static string DefaultTimeZoneOf(Country country) => country switch
    {
        Country.UnitedStates => "America/New_York",
        Country.Germany => "Europe/Berlin",
        Country.Switzerland => "Europe/Zurich",
        _ => "Europe/Vienna",
    };

    /// <summary>The time zones a club can pick (IANA ids), the ones of its country first.</summary>
    public static readonly IReadOnlyList<string> SupportedTimeZones =
    [
        "Europe/Vienna", "Europe/Berlin", "Europe/Zurich",
        "America/New_York", "America/Chicago", "America/Denver", "America/Phoenix", "America/Los_Angeles", "America/Anchorage", "Pacific/Honolulu",
    ];

    public static string CurrencySymbol(string currency) => currency.ToLowerInvariant() switch
    {
        "eur" => "€",
        "usd" => "$",
        "chf" => "CHF",
        var other => other.ToUpperInvariant(),
    };

    // ---- Patterns that the standard format strings don't cover ----

    /// <summary>"16.11." / "11/16"</summary>
    public string DayMonthPattern => IsUnitedStates ? "M/d" : "dd.MM.";

    /// <summary>"16.11.26" / "11/16/26"</summary>
    public string ShortDateTwoDigitYearPattern => IsUnitedStates ? "M/d/yy" : "dd.MM.yy";

    /// <summary>"Mo, 16.11." / "Mon, 11/16"</summary>
    public string WeekdayDayMonthPattern => IsUnitedStates ? "ddd, M/d" : "ddd, dd.MM.";

    /// <summary>"Mo 16.11." / "Mon 11/16"</summary>
    public string CompactWeekdayDayMonthPattern => IsUnitedStates ? "ddd M/d" : "ddd d.M.";

    /// <summary>"Mo 16 Nov" / "Mon Nov 16"</summary>
    public string CompactWeekdayDayMonthNamePattern => IsUnitedStates && !IsGerman ? "ddd MMM d" : "ddd d MMM";

    /// <summary>"16. Nov" / "16 Nov" / "Nov 16"</summary>
    public string DayMonthNamePattern => IsUnitedStates && !IsGerman ? "MMM d" : IsGerman ? "d. MMM" : "d MMM";

    /// <summary>"Mo, 16. Nov" / "Mon, 16 Nov" / "Mon, Nov 16"</summary>
    public string WeekdayDayMonthNamePattern => $"ddd, {DayMonthNamePattern}";

    /// <summary>"16. Nov 2026" / "16 Nov 2026" / "Nov 16, 2026"</summary>
    public string DayMonthNameYearPattern => IsUnitedStates && !IsGerman ? "MMM d, yyyy" : $"{DayMonthNamePattern} yyyy";

    /// <summary>"16. November 2026" / "16 November 2026" / "November 16, 2026"</summary>
    public string LongDayMonthYearPattern => IsUnitedStates && !IsGerman ? "MMMM d, yyyy" : IsGerman ? "d. MMMM yyyy" : "d MMMM yyyy";

    public string Time(TimeOnly time) => time.ToString(DateTimeFormat.ShortTimePattern, this);
    public string Time(DateTime time) => time.ToString(DateTimeFormat.ShortTimePattern, this);
    public string Time(DateTimeOffset time) => time.ToString(DateTimeFormat.ShortTimePattern, this);

    /// <summary>A time of day given as a <see cref="TimeSpan"/>; 24:00 stays "24:00" (or "12:00 AM").</summary>
    public string Time(TimeSpan time)
        => time >= TimeSpan.FromDays(1)
            ? (UsesTwelveHourClock ? Time(TimeOnly.MinValue) : "24:00")
            : Time(TimeOnly.FromTimeSpan(time));

    public string Date(DateOnly date) => date.ToString(DateTimeFormat.ShortDatePattern, this);
    public string Date(DateTime date) => date.ToString(DateTimeFormat.ShortDatePattern, this);
    public string Date(DateTimeOffset date) => date.ToString(DateTimeFormat.ShortDatePattern, this);

    public string DateAndTime(DateTime value) => $"{Date(value)} {Time(value)}";
    public string DateAndTime(DateTimeOffset value) => $"{Date(value)} {Time(value)}";

    /// <summary>Label of a full hour on a time axis: "08" / "8 AM".</summary>
    public string HourLabel(int hour)
        => UsesTwelveHourClock
            ? $"{(hour % 12 == 0 ? 12 : hour % 12)} {(hour % 24 < 12 ? "AM" : "PM")}"
            : $"{hour:00}";

    /// <summary>Label of a full hour where there is room for it: "08:00" / "8 AM".</summary>
    public string FullHourLabel(int hour)
        => UsesTwelveHourClock ? HourLabel(hour) : Time(TimeSpan.FromHours(hour));

    /// <summary>An amount in cents in the club's currency, e.g. "€ 12,50" or "$12.50".</summary>
    public string Money(long cents, string? currency = null) => Money(cents / 100m, currency);

    public string Money(decimal amount, string? currency = null)
    {
        if (currency is null || string.Equals(currency, Currency, StringComparison.OrdinalIgnoreCase))
            return amount.ToString("C", this);

        var format = (NumberFormatInfo)NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol(currency);
        return amount.ToString("C", format);
    }

    /// <summary>The days of a week in the order of the club's calendar.</summary>
    public IReadOnlyList<DayOfWeek> WeekDays
        => Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)DateTimeFormat.FirstDayOfWeek + i) % 7)).ToList();

    /// <summary>The first day of the week that contains <paramref name="date"/>.</summary>
    public DateOnly StartOfWeek(DateOnly date)
        => date.AddDays(-(((int)date.DayOfWeek - (int)DateTimeFormat.FirstDayOfWeek + 7) % 7));

    public override bool Equals(object? value)
        => value is RegionalCulture other && other.Name == Name && other.Country == Country && other.Currency == Currency && other.IsGerman == IsGerman;

    public override int GetHashCode() => HashCode.Combine(Name, Country, Currency, IsGerman);
}
