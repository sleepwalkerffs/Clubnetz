using System.Globalization;
using System.Numerics;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.MoneyAccountKind</c>.</summary>
public enum MoneyAccountKind
{
    Bank = 0,

    /// <summary>The cash box (Kassa).</summary>
    Cash = 1,

    /// <summary>The balance at an online payment provider (Stripe) before it is paid out to the bank.</summary>
    PaymentProvider = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingCategoryKind</c>.</summary>
public enum AccountingCategoryKind
{
    Income = 0,
    Expense = 1,
}

/// <summary>
/// The tax sphere of a non-profit club in Austria (§§ 34 ff. BAO, Vereinsrichtlinien), so the treasurer sees how much each area
/// earns. Keep in sync with <c>Bookennis.Shared.Controller.Accounting.TaxSphere</c>.
/// </summary>
public enum TaxSphere
{
    /// <summary>Ideeller Bereich: membership fees, donations, subsidies, the sport itself.</summary>
    NonProfit = 0,

    /// <summary>Vermögensverwaltung: interest, rent.</summary>
    AssetManagement = 1,

    /// <summary>Hilfsbetrieb: the clubhouse/canteen, small club parties, court hire to guests.</summary>
    AuxiliaryOperation = 2,

    /// <summary>Gewerblicher Betrieb: advertising, sponsoring with something in return, large events.</summary>
    CommercialOperation = 3,
}

/// <summary>What money accounts, categories and cost centers share: a name and archiving instead of deleting once used.</summary>
public abstract class AccountingMasterData : TenantDomainEntity, IAggregateRoot
{
    public string Name { get; protected set; } = "";

    /// <summary>Kept for the entries that use it, but not offered for new ones.</summary>
    public bool IsArchived { get; protected set; }

    public void SetArchived(bool isArchived) => IsArchived = isArchived;

    public void EnsureUsable()
    {
        if (IsArchived)
            throw new PreconditionException(AccountingErrorCode.AccountingArchived, [Name], "It is archived.");
    }

    protected static string ValidName(string? name, int maxLength)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
            throw new PreconditionException(AccountingErrorCode.AccountingNameInvalid, [maxLength.ToString(CultureInfo.InvariantCulture)], "Invalid name.");
        return trimmed;
    }
}

/// <summary>Where the club's money is: a bank account, the cash box or the balance at a payment provider.</summary>
public class MoneyAccount : AccountingMasterData
{
    public const int MaxNameLength = 60;

    private MoneyAccount() { }

    public MoneyAccount(int clubId, AccountData data)
    {
        ClubId = clubId;
        Update(data);
    }

    public record AccountData(string? Name, MoneyAccountKind Kind, string? Iban, int SortOrder);

    public MoneyAccountKind Kind { get; private set; }

    /// <summary>The IBAN of the club's bank account, without spaces, upper case. Only for bank accounts.</summary>
    public string? Iban { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(AccountData data)
    {
        if (!Enum.IsDefined(data.Kind))
            throw new ArgumentOutOfRangeException(nameof(data), data.Kind, "Unknown kind.");

        Name = ValidName(data.Name, MaxNameLength);
        Kind = data.Kind;
        Iban = data.Kind == MoneyAccountKind.Bank ? IbanNumber.Normalize(data.Iban) : null;
        SortOrder = data.SortOrder;
    }
}

/// <summary>What an income or an expense is for (Buchungskategorie), e.g. "Mitgliedsbeiträge" or "Platzpflege".</summary>
public class AccountingCategory : AccountingMasterData
{
    public const int MaxNameLength = 80;
    public const int MaxNumberLength = 10;

    private AccountingCategory() { }

    public AccountingCategory(int clubId, CategoryData data)
    {
        ClubId = clubId;
        Update(data, isUsed: false);
    }

    public record CategoryData(AccountingCategoryKind Kind, string? Number, string? Name, TaxSphere TaxSphere);

    public AccountingCategoryKind Kind { get; private set; }

    /// <summary>Optional number of the treasurer's chart of accounts ("4000"), sorts the categories in the reports.</summary>
    public string? Number { get; private set; }

    public TaxSphere TaxSphere { get; private set; }

    /// <summary>
    /// Changes the category. When <paramref name="isUsed"/> (an entry uses it) it can't change from income to expense: that would turn
    /// the sums of past entries around. When <paramref name="isUsedInClosedYear"/> its tax sphere stays too: the reports of a closed
    /// (and maybe audited) year must not change afterwards.
    /// </summary>
    public void Update(CategoryData data, bool isUsed, bool isUsedInClosedYear = false)
    {
        if (!Enum.IsDefined(data.Kind))
            throw new ArgumentOutOfRangeException(nameof(data), data.Kind, "Unknown kind.");
        if (!Enum.IsDefined(data.TaxSphere))
            throw new ArgumentOutOfRangeException(nameof(data), data.TaxSphere, "Unknown tax sphere.");

        var number = string.IsNullOrWhiteSpace(data.Number) ? null : data.Number.Trim();
        if (number?.Length > MaxNumberLength)
            throw new PreconditionException(AccountingErrorCode.AccountingCategoryNumberInvalid, [MaxNumberLength.ToString(CultureInfo.InvariantCulture)], "Invalid number.");

        var name = ValidName(data.Name, MaxNameLength);
        if (isUsed && data.Kind != Kind)
            throw new PreconditionException(AccountingErrorCode.AccountingCategoryKindInUse, [Name], "The category is used.");
        if (isUsedInClosedYear && data.TaxSphere != TaxSphere)
            throw new PreconditionException(AccountingErrorCode.AccountingCategorySphereInUse, [Name], "The category is used in a closed fiscal year.");

        Kind = data.Kind;
        Number = number;
        Name = name;
        TaxSphere = data.TaxSphere;
    }
}

/// <summary>A part of the club the money is for (Kostenstelle): youth, a team, the club championship, a court renovation.</summary>
public class CostCenter : AccountingMasterData
{
    public const int MaxNameLength = 60;

    private CostCenter() { }

    public CostCenter(int clubId, string? name)
    {
        ClubId = clubId;
        Rename(name);
    }

    public void Rename(string? name) => Name = ValidName(name, MaxNameLength);
}

/// <summary>Checks and formats an IBAN (ISO 13616: country, check digits, mod 97).</summary>
public static class IbanNumber
{
    /// <summary>The IBAN without spaces in upper case, <c>null</c> for an empty one; throws for an invalid one.</summary>
    public static string? Normalize(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return null;

        var compact = new string(iban.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        if (!IsValid(compact))
            throw new PreconditionException(AccountingErrorCode.AccountingIbanInvalid, "Invalid IBAN.");
        return compact;
    }

    public static bool IsValid(string compact)
    {
        if (compact.Length is < 15 or > 34 || !char.IsAsciiLetterUpper(compact[0]) || !char.IsAsciiLetterUpper(compact[1])
            || !char.IsAsciiDigit(compact[2]) || !char.IsAsciiDigit(compact[3]) || !compact.All(char.IsAsciiLetterOrDigit))
            return false;

        var rearranged = compact[4..] + compact[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture)));
        return BigInteger.Parse(digits, CultureInfo.InvariantCulture) % 97 == 1;
    }

    /// <summary>In groups of four, as printed on a bank card.</summary>
    public static string Format(string compact) => string.Join(' ', compact.Chunk(4).Select(c => new string(c)));
}
