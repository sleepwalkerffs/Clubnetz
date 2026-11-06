using System.Globalization;
using Bookennis.Client.Utils;
using Bookennis.Shared.Controller.Accounting;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Pages.ClubShell.Accounting;

/// <summary>Who sees the finances, amounts, dates and the names of accounts and categories, shared by the pages and dialogs of the accounting.</summary>
public static class AccountingHelpers
{
    /// <summary>Treasurers, admins and auditors (same as the backend policy <c>AccountingReader</c>).</summary>
    public static bool CanRead(IEnumerable<MemberRole> roles) => roles.Any(r => r is MemberRole.Admin or MemberRole.Treasurer or MemberRole.Auditor);

    /// <summary>Treasurers and admins keep the books (same as the backend policy <c>AccountingManager</c>).</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles) => roles.Any(r => r is MemberRole.Admin or MemberRole.Treasurer);

    public static string Url(int clubId) => $"/clubs/{clubId}/finances";

    /// <summary>"€ 1.234,56" (the club's books are in euros).</summary>
    public static string Amount(long cents) => "€ " + (cents / 100m).ToString("N2", CultureInfo.CurrentCulture);

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

    public static string Date(DateOnly date) => date.ToString(DateFormats.Localized("d. MMM yyyy"), CultureInfo.CurrentCulture);

    public static string ShortDate(DateOnly date) => date.ToString(DateFormats.Localized("d. MMM"), CultureInfo.CurrentCulture);

    public static string DateTime(DateTimeOffset value) => value.ToLocalTime().ToString(DateFormats.Localized("d. MMM yyyy, HH:mm"), CultureInfo.CurrentCulture);

    /// <summary>
    /// Euros typed by the treasurer to cents (rounded to the cent). Amounts beyond what a number of cents can hold are capped: the
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
        MoneyAccountKind.Cash => "💶",
        _ => "💳",
    };

    public static string FileSize(int bytes) => bytes >= 1024 * 1024
        ? (bytes / (1024m * 1024m)).ToString("0.0", CultureInfo.CurrentCulture) + " MB"
        : Math.Max(1, bytes / 1024).ToString(CultureInfo.CurrentCulture) + " KB";

    /// <summary>The IBAN in groups of four.</summary>
    public static string Iban(string iban) => string.Join(' ', iban.Chunk(4).Select(c => new string(c)));
}

/// <summary>The tabs of the finances page.</summary>
public enum AccountingTab
{
    Overview,
    Entries,
    Receipts,
    Reconcile,
    Reports,
    Settings,
}
