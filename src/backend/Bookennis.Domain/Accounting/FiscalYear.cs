using System.Globalization;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.FiscalYearStatus</c>.</summary>
public enum FiscalYearStatus
{
    Open = 0,

    /// <summary>Locked: no entry can be added, changed or cancelled. The closing balances are the opening balances of the next year.</summary>
    Closed = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.FiscalYearAuditStatus</c>.</summary>
public enum FiscalYearAuditStatus
{
    NotAudited = 0,
    Audited = 1,

    /// <summary>Audited, the auditor found something (see the comment).</summary>
    AuditedWithRemarks = 2,
}

/// <summary>
/// A fiscal year of the club (12 months from the start month of <see cref="AccountingSettings"/>). Numbers the entries made in it,
/// holds the opening balances of the money accounts, is closed by the treasurer and audited by the auditors (Rechnungsprüfer).
/// </summary>
public class FiscalYear : TenantDomainEntity, IAggregateRoot
{
    public const int MaxAuditCommentLength = 2000;

    private readonly List<FiscalYearOpeningBalance> openingBalances = new();

    private FiscalYear() { }

    private FiscalYear(int clubId, DateOnly startDate)
    {
        ClubId = clubId;
        StartDate = startDate;
        EndDate = startDate.AddYears(1).AddDays(-1);
        Status = FiscalYearStatus.Open;
        NextEntryNumber = 1;
        AuditStatus = FiscalYearAuditStatus.NotAudited;
    }

    /// <summary>The first fiscal year of the club in Clubnetz: the one that starts in <paramref name="startYear"/>.</summary>
    public static FiscalYear First(int clubId, int startMonth, int startYear, DateOnly today)
    {
        if (startMonth is < 1 or > 12 || startYear < today.Year - 10 || startYear > today.Year + 1)
            throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearStartInvalid, "Invalid first fiscal year.");

        return new FiscalYear(clubId, new DateOnly(startYear, startMonth, 1));
    }

    /// <summary>
    /// The year after this one. Created by hand it may start at most a year from today; closing a year always creates the next one
    /// (<paramref name="closing"/>), also when the club closes a year ahead of time.
    /// </summary>
    public FiscalYear CreateNext(DateOnly today, bool closing = false)
    {
        var start = EndDate.AddDays(1);
        if (!closing && start > today.AddYears(1))
            throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearTooFarAhead, [start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)], "The next fiscal year starts more than a year from now.");

        return new FiscalYear(ClubId, start);
    }

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalYearStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public int? ClosedByMemberId { get; private set; }

    /// <summary>The number the next entry of this year gets. Numbers are never reused, also not when an entry is cancelled.</summary>
    public int NextEntryNumber { get; private set; }

    /// <summary>
    /// <c>true</c> once the year before carries its balances into this one: the money accounts it used start this year with its closing
    /// balances (<see cref="FiscalYearOpeningBalance.IsCarried"/>). Kept up to date with every change in the year before.
    /// </summary>
    public bool OpeningBalancesCarried { get; private set; }

    public FiscalYearAuditStatus AuditStatus { get; private set; }
    public DateTimeOffset? AuditedAt { get; private set; }
    public int? AuditedByMemberId { get; private set; }
    public string? AuditComment { get; private set; }

    public IReadOnlyList<FiscalYearOpeningBalance> OpeningBalances => openingBalances;

    public bool IsOpen => Status == FiscalYearStatus.Open;

    /// <summary>"2026" for a calendar year, "2025/26" for a year that starts in another month.</summary>
    public string Label => StartDate.Month == 1
        ? StartDate.Year.ToString(CultureInfo.InvariantCulture)
        : $"{StartDate.Year}/{(EndDate.Year % 100):00}";

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public void EnsureOpen()
    {
        if (!IsOpen)
            throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearClosed, [Label], "The fiscal year is closed.");
    }

    /// <summary>Takes the next entry number. Callers hold the accounting lock of the club, so two entries never get the same one.</summary>
    public int TakeEntryNumber()
    {
        EnsureOpen();
        return NextEntryNumber++;
    }

    public int OpeningBalanceCents(int moneyAccountId) => openingBalances.FirstOrDefault(b => b.MoneyAccountId == moneyAccountId)?.AmountCents ?? 0;

    /// <summary>
    /// Sets the opening balance of a money account by hand while the year is open: what was on it before the club used Clubnetz for it.
    /// Not for an account the year before carries its closing balance into.
    /// </summary>
    public void SetOpeningBalance(int moneyAccountId, int amountCents)
    {
        EnsureOpen();
        if (openingBalances.Any(b => b.MoneyAccountId == moneyAccountId && b.IsCarried))
            throw new PreconditionException(AccountingErrorCode.AccountingOpeningBalancesCarried, "The opening balances were carried over from the year before.");
        if (Math.Abs((long)amountCents) > AccountingEntry.MaxAmountCents)
            throw new PreconditionException(AccountingErrorCode.AccountingAmountInvalid, [(AccountingEntry.MaxAmountCents / 100).ToString(CultureInfo.InvariantCulture)], "Invalid amount.");

        SetBalance(moneyAccountId, amountCents);
    }

    /// <summary>
    /// The year before carries its closing balances into this one: <paramref name="closingBalances"/> holds every money account it used
    /// (with an opening balance or entries). Those start this year with their closing balance; a balance carried earlier for an account
    /// the year before no longer uses is dropped, and balances set by hand for other accounts stay.
    /// </summary>
    public void CarryOver(IReadOnlyDictionary<int, int> closingBalances)
    {
        EnsureOpen();
        openingBalances.RemoveAll(b => b.IsCarried && !closingBalances.ContainsKey(b.MoneyAccountId));
        foreach (var (accountId, amount) in closingBalances)
            SetBalance(accountId, amount, carried: true);
        OpeningBalancesCarried = true;
    }

    private void SetBalance(int moneyAccountId, int amountCents, bool carried = false)
    {
        var balance = openingBalances.FirstOrDefault(b => b.MoneyAccountId == moneyAccountId);
        if (balance is null)
            openingBalances.Add(new FiscalYearOpeningBalance(moneyAccountId, amountCents, carried));
        else
            balance.Change(amountCents, carried);
    }

    /// <summary>
    /// Closes the year. <paramref name="previous"/> is the year before (if the club has it in Clubnetz): it must be closed first.
    /// The caller carries the closing balances over into the next year.
    /// </summary>
    public void Close(FiscalYear? previous, int? byMemberId, DateTimeOffset now)
    {
        EnsureOpen();
        if (previous is { IsOpen: true })
            throw new PreconditionException(AccountingErrorCode.AccountingPreviousYearOpen, [previous.Label], "Close the year before first.");

        Status = FiscalYearStatus.Closed;
        ClosedAt = now;
        ClosedByMemberId = byMemberId;
    }

    /// <summary>Opens a closed year again, e.g. to correct an entry. The year after must still be open. An audit no longer counts.</summary>
    public void Reopen(FiscalYear? next)
    {
        if (IsOpen)
            throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearNotClosed, "The fiscal year is open.");
        if (next is { IsOpen: false })
            throw new PreconditionException(AccountingErrorCode.AccountingNextYearClosed, [next.Label], "The year after is closed.");

        Status = FiscalYearStatus.Open;
        ClosedAt = null;
        ClosedByMemberId = null;
        AuditStatus = FiscalYearAuditStatus.NotAudited;
        AuditedAt = null;
        AuditedByMemberId = null;
        AuditComment = null;
    }

    /// <summary>The auditors record their audit of a closed year (or take it back with <see cref="FiscalYearAuditStatus.NotAudited"/>).</summary>
    public void RecordAudit(FiscalYearAuditStatus status, string? comment, int auditorMemberId, DateTimeOffset now)
    {
        if (IsOpen)
            throw new PreconditionException(AccountingErrorCode.AccountingFiscalYearNotClosed, "Only a closed fiscal year can be audited.");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown audit status.");

        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (text?.Length > MaxAuditCommentLength)
            throw new PreconditionException(AccountingErrorCode.AccountingCommentInvalid, [MaxAuditCommentLength.ToString(CultureInfo.InvariantCulture)], "The comment is too long.");

        AuditStatus = status;
        AuditComment = text;
        AuditedAt = status == FiscalYearAuditStatus.NotAudited ? null : now;
        AuditedByMemberId = status == FiscalYearAuditStatus.NotAudited ? null : auditorMemberId;
    }
}

/// <summary>The balance a money account starts a fiscal year with.</summary>
public class FiscalYearOpeningBalance
{
    private FiscalYearOpeningBalance() { }

    internal FiscalYearOpeningBalance(int moneyAccountId, int amountCents, bool isCarried)
    {
        MoneyAccountId = moneyAccountId;
        AmountCents = amountCents;
        IsCarried = isCarried;
    }

    public int Id { get; private set; }
    public int FiscalYearId { get; private set; }
    public int MoneyAccountId { get; private set; }
    public int AmountCents { get; private set; }

    /// <summary>The closing balance of the year before (can't be changed by hand), not one set by hand.</summary>
    public bool IsCarried { get; private set; }

    internal void Change(int amountCents, bool isCarried)
    {
        AmountCents = amountCents;
        IsCarried = isCarried;
    }
}
