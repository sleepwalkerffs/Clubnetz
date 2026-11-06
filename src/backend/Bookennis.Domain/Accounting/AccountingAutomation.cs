using System.Globalization;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>Where money comes from that Clubnetz already knows of. Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingSource</c>.</summary>
public enum AccountingSource
{
    /// <summary>Paid membership orders (online, bank transfer, cash), refunds and disputes of online payments.</summary>
    MembershipFees = 0,

    /// <summary>Court bookings bought online, refunds and disputes.</summary>
    CourtSales = 1,

    /// <summary>The paid clubhouse tabs of a day, per way of paying (for clubs that run the canteen themselves).</summary>
    Clubhouse = 2,

    /// <summary>The fees Stripe keeps and the payouts from the Stripe balance to the bank.</summary>
    Stripe = 3,
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingAutomationMode</c>.</summary>
public enum AccountingAutomationMode
{
    Off = 0,

    /// <summary>Every payment becomes a suggestion the treasurer books (or dismisses).</summary>
    Review = 1,

    /// <summary>Every payment is booked right away; only what can't be booked (e.g. no open fiscal year) waits as a suggestion.</summary>
    Book = 2,
}

/// <summary>
/// Whether and how the payments of one <see cref="AccountingSource"/> become entries: from <see cref="Since"/> on, with
/// <see cref="CategoryId"/> (the income category; for <see cref="AccountingSource.Stripe"/> the expense category of the fees).
/// No row = off.
/// </summary>
public class AccountingSourceSetting : TenantDomainEntity, IAggregateRoot
{
    private AccountingSourceSetting() { }

    public AccountingSourceSetting(int clubId, AccountingSource source)
    {
        if (!Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown source.");

        ClubId = clubId;
        Source = source;
        Mode = AccountingAutomationMode.Off;
    }

    public AccountingSource Source { get; private set; }
    public AccountingAutomationMode Mode { get; private set; }

    /// <summary>Payments before this day are left alone (the treasurer booked them by hand).</summary>
    public DateOnly Since { get; private set; }

    public int? CategoryId { get; private set; }

    public bool IsOn => Mode != AccountingAutomationMode.Off;

    /// <summary>Switches the source on or off. A source that is on needs a category of the right kind (checked by the caller).</summary>
    public void Change(AccountingAutomationMode mode, DateOnly since, int? categoryId)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown mode.");
        if (mode != AccountingAutomationMode.Off && categoryId is null)
            throw new PreconditionException(AccountingErrorCode.AccountingAutomationIncomplete, "A source that is on needs a category.");

        Mode = mode;
        Since = since;
        CategoryId = categoryId;
    }

    /// <summary>The kind the category of the source must have: fees are an expense, everything else income.</summary>
    public static AccountingCategoryKind CategoryKindOf(AccountingSource source)
        => source == AccountingSource.Stripe ? AccountingCategoryKind.Expense : AccountingCategoryKind.Income;
}

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Accounting.AccountingProposalStatus</c>.</summary>
public enum AccountingProposalStatus
{
    /// <summary>Waits for the treasurer (review mode, or it could not be booked by itself).</summary>
    Open = 0,
    Booked = 1,

    /// <summary>The treasurer decided it is no entry (e.g. booked by hand already). It is never suggested again.</summary>
    Dismissed = 2,
}

/// <summary>
/// An entry Clubnetz suggests for a payment it knows of (Buchungsvorschlag). <see cref="SourceKey"/> names the payment
/// (<c>membership-order:12</c>, <c>clubhouse:2026-05-01:cash</c>, ...) and is unique per club, so a payment is never suggested twice -
/// whether the suggestion was booked, dismissed or is still open.
/// </summary>
public class AccountingProposal : TenantDomainEntity, IAggregateRoot
{
    public const int MaxSourceKeyLength = 100;
    public const int MaxProblemLength = 100;

#pragma warning disable CS8618
    private AccountingProposal() { }
#pragma warning restore CS8618

    public AccountingProposal(int clubId, AccountingSource source, string sourceKey, ProposalData data, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || sourceKey.Length > MaxSourceKeyLength)
            throw new ArgumentException("Invalid source key.", nameof(sourceKey));
        if (data.Kind == AccountingEntryKind.Booking ? data.CategoryId is null || data.AmountCents == 0 : data.ToMoneyAccountId is null || data.AmountCents <= 0)
            throw new ArgumentException("Incomplete proposal.", nameof(data));

        ClubId = clubId;
        Source = source;
        SourceKey = sourceKey;
        Date = data.Date;
        Text = data.Text.Length > AccountingEntry.MaxTextLength ? data.Text[..AccountingEntry.MaxTextLength] : data.Text;
        Kind = data.Kind;
        MoneyAccountId = data.MoneyAccountId;
        ToMoneyAccountId = data.ToMoneyAccountId;
        CategoryId = data.CategoryId;
        AmountCents = data.AmountCents;
        CounterpartyMemberId = data.CounterpartyMemberId;
        CounterpartyName = data.CounterpartyName?.Length > AccountingEntry.MaxCounterpartyLength ? data.CounterpartyName[..AccountingEntry.MaxCounterpartyLength] : data.CounterpartyName;
        Status = AccountingProposalStatus.Open;
        CreatedAt = now;
    }

    /// <summary>The suggested entry. <c>AmountCents</c> is in the direction of the category (negative = refund) for a booking, the positive amount of a transfer.</summary>
    public record ProposalData(
        DateOnly Date,
        string Text,
        AccountingEntryKind Kind,
        int MoneyAccountId,
        int? ToMoneyAccountId,
        int? CategoryId,
        int AmountCents,
        int? CounterpartyMemberId,
        string? CounterpartyName);

    public AccountingSource Source { get; private set; }
    public string SourceKey { get; private set; }
    public DateOnly Date { get; private set; }
    public string Text { get; private set; }
    public AccountingEntryKind Kind { get; private set; }
    public int MoneyAccountId { get; private set; }
    public int? ToMoneyAccountId { get; private set; }
    public int? CategoryId { get; private set; }
    public int AmountCents { get; private set; }
    public int? CounterpartyMemberId { get; private set; }
    public string? CounterpartyName { get; private set; }
    public AccountingProposalStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The entry it was booked as.</summary>
    public int? EntryId { get; private set; }

    /// <summary>Why it could not be booked by itself: the error code (e.g. <c>AccountingFiscalYearMissing</c>), shown to the treasurer.</summary>
    public string? Problem { get; private set; }

    public bool IsOpen => Status == AccountingProposalStatus.Open;

    /// <summary>The entry as suggested (the treasurer may change it before booking).</summary>
    public AccountingEntry.EntryData ToEntryData() => new(
        Date,
        Text,
        Kind,
        MoneyAccountId,
        ToMoneyAccountId,
        Kind == AccountingEntryKind.Transfer ? AmountCents : null,
        Kind == AccountingEntryKind.Booking ? [new AccountingEntry.LineData(CategoryId!.Value, null, AmountCents)] : [],
        CounterpartyMemberId,
        CounterpartyName);

    public void EnsureOpen()
    {
        if (!IsOpen)
            throw new PreconditionException(AccountingErrorCode.AccountingProposalNotOpen, "The suggestion was booked or dismissed already.");
    }

    public void Booked(AccountingEntry entry)
    {
        EnsureOpen();
        Status = AccountingProposalStatus.Booked;
        EntryId = entry.Id;
        Problem = null;
    }

    public void Dismiss()
    {
        EnsureOpen();
        Status = AccountingProposalStatus.Dismissed;
        Problem = null;
    }

    /// <summary>Booking it by itself failed: it stays open with the reason.</summary>
    public void CouldNotBook(string errorCode)
        => Problem = errorCode.Length > MaxProblemLength ? errorCode[..MaxProblemLength] : errorCode;

    /// <summary>The source key of a payment, e.g. <c>Key("membership-order", 12)</c> or <c>Key("clubhouse", date, "cash")</c>.</summary>
    public static string Key(string kind, params object[] parts)
        => string.Join(':', new[] { kind }.Concat(parts.Select(p => p switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => p.ToString() ?? "",
        })));
}
