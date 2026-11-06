using System.Globalization;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.BankStatementFormat</c>.</summary>
public enum BankStatementFormat
{
    /// <summary>ISO 20022 bank to customer statement (XML), any version.</summary>
    Camt053 = 0,

    /// <summary>SWIFT MT940 (text).</summary>
    Mt940 = 1,

    /// <summary>A CSV export of online banking: the columns are recognized or mapped once per money account.</summary>
    Csv = 2,
}

/// <summary>One upload of a bank statement file into a money account. Keeps what the file said about the balances for the check.</summary>
public class BankStatementImport : TenantDomainEntity, IAggregateRoot
{
    public const int MaxFileNameLength = 150;

#pragma warning disable CS8618
    private BankStatementImport() { }
#pragma warning restore CS8618

    public BankStatementImport(int clubId, int moneyAccountId, string? fileName, BankStatementFormat format, BankStatement statement, int imported, int skipped, int? byMemberId, DateTimeOffset now)
    {
        ClubId = clubId;
        MoneyAccountId = moneyAccountId;
        var name = Path.GetFileName(fileName?.Trim() ?? "");
        FileName = name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name;
        Format = format;
        FromDate = statement.Lines.Count > 0 ? statement.Lines.Min(l => l.BookingDate) : null;
        ToDate = statement.Lines.Count > 0 ? statement.Lines.Max(l => l.BookingDate) : null;
        OpeningBalanceCents = statement.OpeningBalanceCents;
        ClosingBalanceCents = statement.ClosingBalanceCents;
        ClosingDate = statement.ClosingDate ?? ToDate;
        ImportedCount = imported;
        SkippedCount = skipped;
        ImportedByMemberId = byMemberId;
        ImportedAt = now;
    }

    public int MoneyAccountId { get; private set; }
    public string FileName { get; private set; }
    public BankStatementFormat Format { get; private set; }
    public DateOnly? FromDate { get; private set; }
    public DateOnly? ToDate { get; private set; }

    /// <summary>The balance the file states at its start; <c>null</c> when it has none (most CSV files).</summary>
    public long? OpeningBalanceCents { get; private set; }

    /// <summary>The balance the file states at <see cref="ClosingDate"/>: compared with the books.</summary>
    public long? ClosingBalanceCents { get; private set; }

    public DateOnly? ClosingDate { get; private set; }

    /// <summary>New lines.</summary>
    public int ImportedCount { get; private set; }

    /// <summary>Lines imported before (overlapping files), left out.</summary>
    public int SkippedCount { get; private set; }

    public int? ImportedByMemberId { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.BankTransactionStatus</c>.</summary>
public enum BankTransactionStatus
{
    /// <summary>Not matched with an entry yet: on the treasurer's list.</summary>
    Open = 0,

    /// <summary>Matched with (or booked as) an entry.</summary>
    Booked = 1,

    /// <summary>The treasurer decided it needs no entry (e.g. booked differently).</summary>
    Ignored = 2,
}

/// <summary>
/// A line of a bank statement. Imported once (<see cref="Fingerprint"/> is unique per money account, so overlapping files add nothing twice),
/// then matched with the entry that books it. An entry is matched with at most one line.
/// </summary>
public class BankTransaction : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNameLength = 140;
    public const int MaxReferenceLength = 500;
    public const int MaxBankReferenceLength = 100;
    public const int MaxFingerprintLength = 80;

#pragma warning disable CS8618
    private BankTransaction() { }
#pragma warning restore CS8618

    public BankTransaction(int clubId, int moneyAccountId, int importId, BankStatementLine line, string fingerprint)
    {
        if (line.AmountCents == 0 || Math.Abs(line.AmountCents) > int.MaxValue)
            throw new ArgumentException("Invalid amount.", nameof(line));

        ClubId = clubId;
        MoneyAccountId = moneyAccountId;
        ImportId = importId;
        BookingDate = line.BookingDate;
        ValueDate = line.ValueDate;
        AmountCents = (int)line.AmountCents;
        CounterpartyName = Cut(line.CounterpartyName, MaxNameLength);
        CounterpartyIban = Cut(line.CounterpartyIban?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant(), 34);
        Reference = Cut(line.Reference, MaxReferenceLength) ?? "";
        BankReference = Cut(line.BankReference, MaxBankReferenceLength);
        Fingerprint = fingerprint;
        Status = BankTransactionStatus.Open;
    }

    public int MoneyAccountId { get; private set; }
    public int ImportId { get; private set; }
    public DateOnly BookingDate { get; private set; }
    public DateOnly? ValueDate { get; private set; }

    /// <summary>Positive = money came in, negative = money went out.</summary>
    public int AmountCents { get; private set; }

    public string? CounterpartyName { get; private set; }
    public string? CounterpartyIban { get; private set; }

    /// <summary>What the line says: payment reference, purpose, booking text.</summary>
    public string Reference { get; private set; }

    /// <summary>The bank's own id of the line (end-to-end id, bank reference), if the file has one.</summary>
    public string? BankReference { get; private set; }

    public string Fingerprint { get; private set; }
    public BankTransactionStatus Status { get; private set; }

    /// <summary>The entry that books this line.</summary>
    public int? EntryId { get; private set; }

    public bool IsOpen => Status == BankTransactionStatus.Open;

    public void EnsureOpen()
    {
        if (!IsOpen)
            throw new PreconditionException(AccountingErrorCode.AccountingBankTransactionNotOpen, "The bank line was matched or ignored already.");
    }

    /// <summary>Matches the line with <paramref name="entry"/>, which must put exactly this amount on the money account of the line.</summary>
    public void Match(AccountingEntry entry, Func<int, AccountingCategoryKind> kindOfCategory)
    {
        EnsureOpen();
        if (entry.ClubId != ClubId || !entry.IsActive || entry.EffectOn(MoneyAccountId, kindOfCategory) != AmountCents)
            throw new PreconditionException(AccountingErrorCode.AccountingBankTransactionAmountMismatch,
                [(AmountCents / 100m).ToString("0.00", CultureInfo.InvariantCulture)], "The entry does not book the amount of the bank line.");

        Status = BankTransactionStatus.Booked;
        EntryId = entry.Id;
    }

    public void Ignore()
    {
        EnsureOpen();
        Status = BankTransactionStatus.Ignored;
    }

    /// <summary>Back onto the list: unmatched (the entry stays) or no longer ignored.</summary>
    public void Reopen()
    {
        Status = BankTransactionStatus.Open;
        EntryId = null;
    }

    /// <summary>
    /// The identity of a line across files: date, amount, reference, counterparty and the bank's id, plus
    /// <paramref name="occurrence"/> for lines that look exactly the same on the same day (two equal payments).
    /// </summary>
    public static string FingerprintOf(BankStatementLine line, int occurrence)
    {
        var text = string.Join('|',
            line.BookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            line.AmountCents.ToString(CultureInfo.InvariantCulture),
            Normalize(line.BankReference),
            Normalize(line.CounterpartyIban),
            Normalize(line.CounterpartyName),
            Normalize(line.Reference));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        return $"{hash[..40]}#{occurrence.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Normalize(string? value)
        => string.Concat((value ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static string? Cut(string? value, int length)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text?.Length > length ? text[..length] : text;
    }
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.BankRuleDirection</c>.</summary>
public enum BankRuleDirection
{
    Any = 0,
    Incoming = 1,
    Outgoing = 2,
}

/// <summary>
/// "Lines of ÖTV are association fees": a bank line that matches (<see cref="MatchText"/> in the name or reference, or the IBAN) is
/// suggested - and with one click booked - with the category, cost center and text of the rule.
/// </summary>
public class BankRule : TenantDomainEntity, IAggregateRoot
{
    public const int MaxMatchTextLength = 100;

#pragma warning disable CS8618
    private BankRule() { }
#pragma warning restore CS8618

    public BankRule(int clubId, RuleData data)
    {
        ClubId = clubId;
        Change(data);
    }

    /// <summary>
    /// <c>MatchText</c> is found (ignoring case) in the counterparty name or the reference of the line, <c>MatchIban</c> is the IBAN of the
    /// counterparty (instead of or with the text). <c>Text</c> is the text of the entry; empty = the name and reference of the line.
    /// </summary>
    public record RuleData(string? MatchText, string? MatchIban, BankRuleDirection Direction, int CategoryId, int? CostCenterId, string? Text, int? MoneyAccountId);

    public string? MatchText { get; private set; }
    public string? MatchIban { get; private set; }
    public BankRuleDirection Direction { get; private set; }
    public int CategoryId { get; private set; }
    public int? CostCenterId { get; private set; }
    public string? Text { get; private set; }

    /// <summary>Only for lines of this money account; <c>null</c> = every bank account.</summary>
    public int? MoneyAccountId { get; private set; }

    public void Change(RuleData data)
    {
        if (!Enum.IsDefined(data.Direction))
            throw new ArgumentOutOfRangeException(nameof(data), data.Direction, "Unknown direction.");

        var match = string.IsNullOrWhiteSpace(data.MatchText) ? null : data.MatchText.Trim();
        var iban = string.IsNullOrWhiteSpace(data.MatchIban) ? null : data.MatchIban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (match is null && iban is null)
            throw new PreconditionException(AccountingErrorCode.AccountingBankRuleInvalid, "A rule needs a text or an IBAN to look for.");
        if (match?.Length > MaxMatchTextLength)
            throw new PreconditionException(AccountingErrorCode.AccountingNameInvalid, [MaxMatchTextLength.ToString(CultureInfo.InvariantCulture)], "The text is too long.");
        if (iban is not null && !IbanNumber.IsValid(iban))
            throw new PreconditionException(AccountingErrorCode.AccountingIbanInvalid, "Invalid IBAN.");

        var text = string.IsNullOrWhiteSpace(data.Text) ? null : data.Text.Trim();
        if (text?.Length > AccountingEntry.MaxTextLength)
            throw new PreconditionException(AccountingErrorCode.AccountingTextInvalid, [AccountingEntry.MaxTextLength.ToString(CultureInfo.InvariantCulture)], "Invalid text.");

        MatchText = match;
        MatchIban = iban;
        Direction = data.Direction;
        CategoryId = data.CategoryId;
        CostCenterId = data.CostCenterId;
        Text = text;
        MoneyAccountId = data.MoneyAccountId;
    }

    public bool Matches(BankTransaction line)
    {
        if (MoneyAccountId is { } account && account != line.MoneyAccountId)
            return false;
        if (Direction == BankRuleDirection.Incoming && line.AmountCents < 0 || Direction == BankRuleDirection.Outgoing && line.AmountCents > 0)
            return false;
        if (MatchIban is not null && !string.Equals(MatchIban, line.CounterpartyIban, StringComparison.OrdinalIgnoreCase))
            return false;
        return MatchText is null
               || (line.CounterpartyName?.Contains(MatchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || line.Reference.Contains(MatchText, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>How the CSV export of a money account's bank is read, when its columns are not recognized by themselves. One per money account.</summary>
public class BankCsvLayout : TenantDomainEntity, IAggregateRoot
{
    private BankCsvLayout() { }

    public BankCsvLayout(int clubId, int moneyAccountId, CsvColumns columns)
    {
        ClubId = clubId;
        MoneyAccountId = moneyAccountId;
        Columns = columns;
    }

    public int MoneyAccountId { get; private set; }

    public CsvColumns Columns { get; private set; } = new();

    public void Change(CsvColumns columns) => Columns = columns;
}
