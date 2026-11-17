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

    /// <summary>
    /// A bank account has an <c>Iban</c> (Europe) or an <c>AccountNumber</c> with its ABA <c>RoutingNumber</c> (United States), or
    /// neither; the other kinds have no bank details.
    /// </summary>
    public record AccountData(string? Name, MoneyAccountKind Kind, string? Iban, int SortOrder, string? AccountNumber = null, string? RoutingNumber = null);

    public MoneyAccountKind Kind { get; private set; }

    /// <summary>The IBAN of the club's bank account, without spaces, upper case. Only for bank accounts.</summary>
    public string? Iban { get; private set; }

    /// <summary>The number of a US bank account (digits only), always with <see cref="RoutingNumber"/>. Only for bank accounts.</summary>
    public string? AccountNumber { get; private set; }

    /// <summary>The 9-digit ABA routing number of the bank of <see cref="AccountNumber"/>.</summary>
    public string? RoutingNumber { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Somebody can pay to it: an IBAN, or a US account number with its routing number.</summary>
    public bool HasBankDetails => Iban is not null || AccountNumber is not null;

    public void Update(AccountData data)
    {
        if (!Enum.IsDefined(data.Kind))
            throw new ArgumentOutOfRangeException(nameof(data), data.Kind, "Unknown kind.");

        Name = ValidName(data.Name, MaxNameLength);
        Kind = data.Kind;
        var bank = data.Kind == MoneyAccountKind.Bank;
        Iban = bank ? IbanNumber.Normalize(data.Iban) : null;
        var accountNumber = bank ? UsBankAccount.NormalizeAccountNumber(data.AccountNumber) : null;
        var routingNumber = bank ? AbaRoutingNumber.Normalize(data.RoutingNumber) : null;
        if (accountNumber is null != routingNumber is null)
            throw new PreconditionException(accountNumber is null ? AccountingErrorCode.AccountingAccountNumberInvalid : AccountingErrorCode.AccountingRoutingNumberInvalid,
                "A US bank account needs both its account number and its routing number.");
        AccountNumber = accountNumber;
        RoutingNumber = routingNumber;
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

/// <summary>Checks the 9-digit ABA routing number of a US bank (weights 3, 7, 1: the sum is a multiple of 10).</summary>
public static class AbaRoutingNumber
{
    public const int Length = 9;

    /// <summary>The digits, <c>null</c> for an empty one; throws for an invalid one.</summary>
    public static string? Normalize(string? routingNumber)
    {
        if (string.IsNullOrWhiteSpace(routingNumber))
            return null;

        var compact = new string(routingNumber.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        if (!IsValid(compact))
            throw new PreconditionException(AccountingErrorCode.AccountingRoutingNumberInvalid, "Invalid routing number.");
        return compact;
    }

    public static bool IsValid(string compact)
    {
        if (compact.Length != Length || !compact.All(char.IsAsciiDigit))
            return false;

        int[] weights = [3, 7, 1, 3, 7, 1, 3, 7, 1];
        var sum = compact.Select((c, i) => (c - '0') * weights[i]).Sum();
        return sum % 10 == 0 && compact != "000000000";
    }
}

/// <summary>The number of a US bank account: 4 to 17 digits (banks print them with spaces or dashes).</summary>
public static class UsBankAccount
{
    public const int MinLength = 4;
    public const int MaxLength = 17;

    public static string? NormalizeAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return null;

        var compact = new string(accountNumber.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        if (compact.Length is < MinLength or > MaxLength || !compact.All(char.IsAsciiDigit))
            throw new PreconditionException(AccountingErrorCode.AccountingAccountNumberInvalid, [MinLength.ToString(CultureInfo.InvariantCulture), MaxLength.ToString(CultureInfo.InvariantCulture)], "Invalid account number.");
        return compact;
    }

    /// <summary>"••••6789": the last four digits, for lists that don't need the whole number.</summary>
    public static string Masked(string accountNumber) => "••••" + accountNumber[^Math.Min(4, accountNumber.Length)..];
}
