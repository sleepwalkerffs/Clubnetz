using System.Globalization;
using Bookennis.Shared.Controller.Accounting;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils;

namespace Bookennis.Client.Pages.ClubShell.Accounting;

/// <summary>Who sees the finances, amounts, dates and the names of accounts and categories, shared by the pages and dialogs of the accounting.</summary>
public static class AccountingHelpers
{
    /// <summary>Treasurers, admins and auditors (same as the backend policy <c>AccountingReader</c>).</summary>
    public static bool CanRead(IEnumerable<MemberRole> roles) => roles.Any(r => r is MemberRole.Admin or MemberRole.Treasurer or MemberRole.Auditor);

    /// <summary>Treasurers and admins keep the books (same as the backend policy <c>AccountingManager</c>).</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles) => roles.Any(r => r is MemberRole.Admin or MemberRole.Treasurer);

    public static string Url(int clubId) => $"/clubs/{clubId}/finances";

    private static Country country = Country.Austria;
    private static string currency = RegionalCulture.DefaultCurrency;
    private static TimeZoneInfo? timeZone;
    private static RegionalCulture? culture;

    /// <summary>
    /// Where the club of the loaded accounting is (<see cref="AccountingDto.Country"/>, currency, time zone): amounts, dates, bank details
    /// and tax spheres follow it. Set by the store whenever it loads the accounting of a club.
    /// </summary>
    public static void UseRegion(Country clubCountry, string? clubCurrency, string? timeZoneId)
    {
        country = clubCountry;
        currency = string.IsNullOrWhiteSpace(clubCurrency) ? RegionalCulture.DefaultCurrencyOf(clubCountry) : clubCurrency.ToLowerInvariant();
        timeZone = timeZoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : null;
        culture = null;
    }

    /// <summary>A club in the United States: account and routing number instead of IBAN, EIN instead of ZVR, no tax spheres, US formats.</summary>
    public static bool IsUnitedStates => country == Country.UnitedStates;

    /// <summary>The formats of the club's country in the language of the app.</summary>
    public static RegionalCulture Culture
    {
        get
        {
            var german = CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "de";
            if (culture is null || culture.IsGerman != german)
                culture = CultureInfo.CurrentCulture is RegionalCulture current && current.Country == country && current.Currency == currency
                    ? current
                    : RegionalCulture.For(german, country, currency);
            return culture;
        }
    }

    /// <summary>"€" or "$": the adornment of the amount fields.</summary>
    public static string CurrencySymbol => RegionalCulture.CurrencySymbol(currency);

    /// <summary>"€ 1.234,56" or "$1,234.56": an amount in the club's currency.</summary>
    public static string Amount(long cents) => Culture.Money(cents);

    /// <summary>"+ € 120,00" / "− € 45,00": the direction of the money.</summary>
    public static string SignedAmount(long cents) => cents switch
    {
        > 0 => "+ " + Amount(cents),
        < 0 => "− " + Amount(-cents),
        _ => Amount(0),
    };

    public static string AmountClass(long cents) => cents switch
    {
        > 0 => "acc-amount acc-amount--in",
        < 0 => "acc-amount acc-amount--out",
        _ => "acc-amount",
    };

    /// <summary>"16. Nov. 2026", "16 Nov 2026" or "Nov 16, 2026".</summary>
    public static string Date(DateOnly date) => date.ToString(Culture.DayMonthNameYearPattern, Culture);

    public static string ShortDate(DateOnly date) => date.ToString(Culture.DayMonthNamePattern, Culture);

    /// <summary>A moment as the wall-clock time of the club ("16. Nov. 2026, 18:30" / "Nov 16, 2026, 6:30 PM").</summary>
    public static string DateTime(DateTimeOffset value)
    {
        var local = timeZone is null ? value.ToLocalTime() : TimeZoneInfo.ConvertTime(value, timeZone);
        return local.ToString($"{Culture.DayMonthNameYearPattern}, {Culture.DateTimeFormat.ShortTimePattern}", Culture);
    }

    /// <summary>
    /// An amount typed by the treasurer to cents (rounded to the cent). Amounts beyond what a number of cents can hold are capped: the
    /// server refuses them with its own message.
    /// </summary>
    public static int ToCents(decimal euros) => (int)Math.Clamp(Math.Round(euros * 100m, MidpointRounding.AwayFromZero), int.MinValue, int.MaxValue);

    public static decimal ToEuros(long cents) => cents / 100m;

    public static string AccountName(AccountingDto? basics, int? id)
        => id is { } accountId ? basics?.MoneyAccounts.FirstOrDefault(a => a.Id == accountId)?.Name ?? "?" : "";

    /// <summary>"4000 · Mitgliedsbeiträge", or the name alone when the category has no number.</summary>
    public static string CategoryName(AccountingDto? basics, int id)
        => basics?.Categories.FirstOrDefault(c => c.Id == id) is { } category ? CategoryName(category) : "?";

    public static string CategoryName(AccountingCategoryDto category) => category.Number is { } number ? $"{number} · {category.Name}" : category.Name;

    public static string CostCenterName(AccountingDto? basics, int? id)
        => id is { } costCenterId ? basics?.CostCenters.FirstOrDefault(c => c.Id == costCenterId)?.Name ?? "?" : "";

    public static string AccountIcon(MoneyAccountKind kind) => kind switch
    {
        MoneyAccountKind.Bank => "🏦",
        MoneyAccountKind.Cash => IsUnitedStates ? "💵" : "💶",
        _ => "💳",
    };

    public static string FileSize(int bytes) => bytes >= 1024 * 1024
        ? (bytes / (1024m * 1024m)).ToString("0.0", CultureInfo.CurrentCulture) + " MB"
        : Math.Max(1, bytes / 1024).ToString(CultureInfo.CurrentCulture) + " KB";

    /// <summary>The IBAN in groups of four.</summary>
    public static string Iban(string iban) => string.Join(' ', iban.Chunk(4).Select(c => new string(c)));

    /// <summary>
    /// The key of a text that a US club reads differently (no Vereinsgesetz, no tax spheres, EIN instead of ZVR): <c>{key}_Us</c> there.
    /// </summary>
    public static string Regional(string key) => IsUnitedStates ? key + "_Us" : key;

    /// <summary>Somebody can pay to it: an IBAN, or a US account number with its routing number.</summary>
    public static bool HasBankDetails(MoneyAccountDto account) => account.Iban is not null || account.AccountNumber is not null;

    /// <summary>"AT61 1904 …" or "••••6789 · 021000021": the bank details in a list.</summary>
    public static string? BankDetails(MoneyAccountDto account)
        => account.Iban is { } iban ? Iban(iban)
            : account.AccountNumber is { } number ? $"••••{number[^Math.Min(4, number.Length)..]} · {account.RoutingNumber}"
            : null;
}

/// <summary>The tabs of the finances page.</summary>
public enum AccountingTab
{
    Overview,
    Entries,
    Receipts,
    Invoices,
    Reconcile,
    Reports,
    Settings,
}
