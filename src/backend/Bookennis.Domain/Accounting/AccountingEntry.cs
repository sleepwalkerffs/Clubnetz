using System.Globalization;
using System.Text.Json;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingEntryKind</c>.</summary>
public enum AccountingEntryKind
{
    /// <summary>Income and/or expense on one money account, split into lines by category.</summary>
    Booking = 0,

    /// <summary>Money moved between two money accounts of the club (bank to cash box): neither income nor expense.</summary>
    Transfer = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingEntryStatus</c>.</summary>
public enum AccountingEntryStatus
{
    Active = 0,

    /// <summary>Kept with its number for the record, but left out of every sum. Entries are never deleted.</summary>
    Cancelled = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingEntryChangeKind</c>.</summary>
public enum AccountingEntryChangeKind
{
    Created = 0,
    Changed = 1,
    Cancelled = 2,
}

/// <summary>
/// One entry of the club's books (Buchung): the money that came in or went out of a money account on a date, split into lines by
/// category and cost center, or a transfer between two money accounts. Numbered per fiscal year. In an open year it can be changed,
/// every version is kept in <see cref="Changes"/>; it is never deleted, only cancelled.
/// </summary>
public class AccountingEntry : TenantDomainEntity, IAggregateRoot
{
    public const int MaxTextLength = 200;
    public const int MaxCounterpartyLength = 120;
    public const int MaxCancelReasonLength = 200;
    public const int MaxLines = 20;

    /// <summary>1 million euros per line - a typo guard for a tennis club.</summary>
    public const int MaxAmountCents = 100_000_000;

    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web);

    private readonly List<AccountingEntryLine> lines = new();
    private readonly List<AccountingEntryChange> changes = new();

#pragma warning disable CS8618
    private AccountingEntry() { }
#pragma warning restore CS8618

    /// <summary>
    /// A new entry in <paramref name="year"/> (open, contains the date). The money accounts, categories and cost centers of
    /// <paramref name="data"/> were checked by the caller (of the club, not archived). <paramref name="receiptIds"/> are the receipts attached
    /// with it, for the history (the receipts point to the entry, see <see cref="AccountingReceipt"/>).
    /// </summary>
    public AccountingEntry(FiscalYear year, EntryData data, IReadOnlyList<int> receiptIds, int? byMemberId, DateTimeOffset now)
    {
        EnsureDateIn(year, data.Date);
        ClubId = year.ClubId;
        FiscalYearId = year.Id;
        Apply(data);
        Number = year.TakeEntryNumber();
        Status = AccountingEntryStatus.Active;
        Log(AccountingEntryChangeKind.Created, byMemberId, now, receiptIds);
    }

    public record EntryData(
        DateOnly Date,
        string? Text,
        AccountingEntryKind Kind,
        int MoneyAccountId,
        int? ToMoneyAccountId,
        int? TransferAmountCents,
        IReadOnlyList<LineData> Lines,
        int? CounterpartyMemberId,
        string? CounterpartyName);

    /// <summary>A line of an income/expense. <c>AmountCents</c> is in the direction of the category: an income adds it to the money account, an expense takes it away. Negative for a refund.</summary>
    public record LineData(int CategoryId, int? CostCenterId, int AmountCents);

    public int FiscalYearId { get; private set; }

    /// <summary>The number in its fiscal year, also the number the receipts are filed under.</summary>
    public int Number { get; private set; }

    public DateOnly Date { get; private set; }
    public string Text { get; private set; } = "";
    public AccountingEntryKind Kind { get; private set; }

    /// <summary>The money account of the income/expense, or where a transfer comes from.</summary>
    public int MoneyAccountId { get; private set; }

    /// <summary>Where a transfer goes to; <c>null</c> for income and expenses.</summary>
    public int? ToMoneyAccountId { get; private set; }

    /// <summary>The amount of a transfer; <c>null</c> for income and expenses (their lines carry the amounts).</summary>
    public int? TransferAmountCents { get; private set; }

    /// <summary>The member who paid or was paid; <c>null</c> for somebody from outside and once the member left the club.</summary>
    public int? CounterpartyMemberId { get; private set; }

    /// <summary>Who paid or was paid, as the treasurer wrote it (or the member's name when the entry was saved): the record stays when the member leaves.</summary>
    public string? CounterpartyName { get; private set; }

    public AccountingEntryStatus Status { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public int? CancelledByMemberId { get; private set; }
    public string? CancelReason { get; private set; }

    public IReadOnlyList<AccountingEntryLine> Lines => lines;
    public IReadOnlyList<AccountingEntryChange> Changes => changes;

    public bool IsActive => Status == AccountingEntryStatus.Active;

    /// <summary>What the entry does to a money account: income minus expenses for its own account, ± the amount of a transfer.</summary>
    public long EffectOn(int moneyAccountId, Func<int, AccountingCategoryKind> kindOfCategory)
    {
        if (!IsActive)
            return 0;

        if (Kind == AccountingEntryKind.Transfer)
        {
            var amount = TransferAmountCents ?? 0;
            return (MoneyAccountId == moneyAccountId ? -amount : 0) + (ToMoneyAccountId == moneyAccountId ? amount : 0);
        }

        return MoneyAccountId != moneyAccountId
            ? 0
            : lines.Sum(l => kindOfCategory(l.CategoryId) == AccountingCategoryKind.Income ? (long)l.AmountCents : -(long)l.AmountCents);
    }

    /// <summary>
    /// Changes the entry in its (open) fiscal year. The date can't leave the year: the number belongs to it.
    /// <paramref name="receiptIds"/> are the receipts attached afterwards, for the history.
    /// </summary>
    public void Update(FiscalYear year, EntryData data, IReadOnlyList<int> receiptIds, int? byMemberId, DateTimeOffset now)
    {
        EnsureChangeable(year);
        EnsureDateIn(year, data.Date);
        Apply(data);
        Log(AccountingEntryChangeKind.Changed, byMemberId, now, receiptIds);
    }

    public void Cancel(FiscalYear year, string? reason, int? byMemberId, DateTimeOffset now)
    {
        EnsureChangeable(year);

        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text?.Length > MaxCancelReasonLength)
            throw new PreconditionException(AccountingErrorCode.AccountingCommentInvalid, [MaxCancelReasonLength.ToString(CultureInfo.InvariantCulture)], "The reason is too long.");

        Status = AccountingEntryStatus.Cancelled;
        CancelledAt = now;
        CancelledByMemberId = byMemberId;
        CancelReason = text;
        Log(AccountingEntryChangeKind.Cancelled, byMemberId, now, null);
    }

    public void EnsureChangeable(FiscalYear year)
    {
        if (year.Id != FiscalYearId)
            throw new ArgumentException("Not the fiscal year of the entry.", nameof(year));
        year.EnsureOpen();
        if (!IsActive)
            throw new PreconditionException(AccountingErrorCode.AccountingEntryCancelled, "The entry was cancelled.");
    }

    private static void EnsureDateIn(FiscalYear year, DateOnly date)
    {
        year.EnsureOpen();
        if (!year.Contains(date))
            throw new PreconditionException(AccountingErrorCode.AccountingDateOutsideFiscalYear, [year.Label], "The date is not in the fiscal year.");
    }

    private void Apply(EntryData data)
    {
        if (!Enum.IsDefined(data.Kind))
            throw new ArgumentOutOfRangeException(nameof(data), data.Kind, "Unknown kind.");

        var text = data.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxTextLength)
            throw new PreconditionException(AccountingErrorCode.AccountingTextInvalid, [MaxTextLength.ToString(CultureInfo.InvariantCulture)], "Invalid text.");

        var counterparty = string.IsNullOrWhiteSpace(data.CounterpartyName) ? null : data.CounterpartyName.Trim();
        if (counterparty?.Length > MaxCounterpartyLength)
            throw new PreconditionException(AccountingErrorCode.AccountingCounterpartyInvalid, [MaxCounterpartyLength.ToString(CultureInfo.InvariantCulture)], "Invalid counterparty.");

        if (data.Kind == AccountingEntryKind.Transfer)
        {
            if (data.ToMoneyAccountId is not { } to || to == data.MoneyAccountId || data.Lines.Count > 0 || data.TransferAmountCents is not > 0)
                throw new PreconditionException(AccountingErrorCode.AccountingTransferInvalid, "Invalid transfer.");
            EnsureAmount(data.TransferAmountCents.Value);
        }
        else
        {
            if (data.Lines.Count is 0 or > MaxLines || data.ToMoneyAccountId is not null || data.TransferAmountCents is not null)
                throw new PreconditionException(AccountingErrorCode.AccountingLinesInvalid, [MaxLines.ToString(CultureInfo.InvariantCulture)], "An income or expense needs lines.");
            foreach (var line in data.Lines)
                EnsureAmount(line.AmountCents);
        }

        Date = data.Date;
        Text = text;
        Kind = data.Kind;
        MoneyAccountId = data.MoneyAccountId;
        ToMoneyAccountId = data.Kind == AccountingEntryKind.Transfer ? data.ToMoneyAccountId : null;
        TransferAmountCents = data.Kind == AccountingEntryKind.Transfer ? data.TransferAmountCents : null;
        CounterpartyMemberId = data.CounterpartyMemberId;
        CounterpartyName = counterparty;

        lines.Clear();
        lines.AddRange(data.Lines.Select((l, i) => new AccountingEntryLine(i, l.CategoryId, l.CostCenterId, l.AmountCents)));
    }

    private static void EnsureAmount(int amountCents)
    {
        if (amountCents == 0 || Math.Abs((long)amountCents) > MaxAmountCents)
            throw new PreconditionException(AccountingErrorCode.AccountingAmountInvalid, [(MaxAmountCents / 100).ToString(CultureInfo.InvariantCulture)], "Invalid amount.");
    }

    private void Log(AccountingEntryChangeKind kind, int? byMemberId, DateTimeOffset now, IReadOnlyList<int>? receiptIds)
    {
        var snapshot = new EntrySnapshot(Date, Text, Kind, MoneyAccountId, ToMoneyAccountId, TransferAmountCents, CounterpartyMemberId, CounterpartyName,
            lines.Select(l => new LineData(l.CategoryId, l.CostCenterId, l.AmountCents)).ToList(), CancelReason, receiptIds);
        changes.Add(new AccountingEntryChange(kind, byMemberId, now, JsonSerializer.Serialize(snapshot, SnapshotOptions)));
    }

    /// <summary>The entry as it was after a change, kept as JSON in <see cref="AccountingEntryChange.Snapshot"/>.</summary>
    public record EntrySnapshot(
        DateOnly Date,
        string Text,
        AccountingEntryKind Kind,
        int MoneyAccountId,
        int? ToMoneyAccountId,
        int? TransferAmountCents,
        int? CounterpartyMemberId,
        string? CounterpartyName,
        IReadOnlyList<LineData> Lines,
        string? CancelReason,
        IReadOnlyList<int>? ReceiptIds)
    {
        public static EntrySnapshot? Read(string json) => JsonSerializer.Deserialize<EntrySnapshot>(json, SnapshotOptions);
    }
}

/// <summary>A part of an income/expense: the amount for one category (and optionally one cost center).</summary>
public class AccountingEntryLine
{
    private AccountingEntryLine() { }

    internal AccountingEntryLine(int position, int categoryId, int? costCenterId, int amountCents)
    {
        Position = position;
        CategoryId = categoryId;
        CostCenterId = costCenterId;
        AmountCents = amountCents;
    }

    public int Id { get; private set; }
    public int AccountingEntryId { get; private set; }

    /// <summary>The order of the lines in the entry.</summary>
    public int Position { get; private set; }

    public int CategoryId { get; private set; }
    public int? CostCenterId { get; private set; }
    public int AmountCents { get; private set; }
}

/// <summary>One version in the history of an entry: who changed it when, and what it looked like afterwards.</summary>
public class AccountingEntryChange
{
#pragma warning disable CS8618
    private AccountingEntryChange() { }
#pragma warning restore CS8618

    internal AccountingEntryChange(AccountingEntryChangeKind kind, int? byMemberId, DateTimeOffset at, string snapshot)
    {
        Kind = kind;
        ByMemberId = byMemberId;
        At = at;
        Snapshot = snapshot;
    }

    public int Id { get; private set; }
    public int AccountingEntryId { get; private set; }
    public AccountingEntryChangeKind Kind { get; private set; }

    /// <summary><c>null</c> in support mode and once the member left the club.</summary>
    public int? ByMemberId { get; private set; }

    public DateTimeOffset At { get; private set; }

    /// <summary>The entry after the change (<see cref="AccountingEntry.EntrySnapshot"/> as JSON).</summary>
    public string Snapshot { get; private set; }
}
