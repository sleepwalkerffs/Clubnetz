namespace Bookennis.Shared.Controller.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingSource</c>.</summary>
public enum AccountingSource
{
    MembershipFees = 0,
    CourtSales = 1,
    Clubhouse = 2,
    Stripe = 3,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingAutomationMode</c>.</summary>
public enum AccountingAutomationMode
{
    Off = 0,
    Review = 1,
    Book = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingProposalStatus</c>.</summary>
public enum AccountingProposalStatus
{
    Open = 0,
    Booked = 1,
    Dismissed = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.BankStatementFormat</c>.</summary>
public enum BankStatementFormat
{
    Camt053 = 0,
    Mt940 = 1,
    Csv = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.BankTransactionStatus</c>.</summary>
public enum BankTransactionStatus
{
    Open = 0,
    Booked = 1,
    Ignored = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.BankRuleDirection</c>.</summary>
public enum BankRuleDirection
{
    Any = 0,
    Incoming = 1,
    Outgoing = 2,
}

/// <summary>Which payments become entries by themselves, and into which money accounts.</summary>
public record AccountingAutomationDto
{
    /// <summary>The Stripe balance of the club (money paid online).</summary>
    public int? OnlineMoneyAccountId { get; init; }

    public int? BankMoneyAccountId { get; init; }
    public int? CashMoneyAccountId { get; init; }

    /// <summary>The card terminal or SumUp account of the clubhouse.</summary>
    public int? CardMoneyAccountId { get; init; }

    public List<AccountingSourceSettingDto> Sources { get; init; } = [];

    /// <summary>The club takes payments with Stripe: membership fees and court sales paid online go to the online account.</summary>
    public required bool StripeConnected { get; init; }
}

public record AccountingSourceSettingDto
{
    public AccountingSource Source { get; init; }
    public AccountingAutomationMode Mode { get; init; }

    /// <summary>Payments before this day are left alone.</summary>
    public DateOnly? Since { get; init; }

    /// <summary>The income category (for Stripe: the expense category of the fees).</summary>
    public int? CategoryId { get; init; }
}

/// <summary>Body of <c>PUT Accounting/automation</c>.</summary>
public record SaveAccountingAutomationModel
{
    public int? OnlineMoneyAccountId { get; init; }
    public int? BankMoneyAccountId { get; init; }
    public int? CashMoneyAccountId { get; init; }
    public int? CardMoneyAccountId { get; init; }
    public List<AccountingSourceSettingDto> Sources { get; init; } = [];
}

/// <summary>An entry Clubnetz suggests for a payment it knows of.</summary>
public record AccountingProposalDto
{
    public required int Id { get; init; }
    public required AccountingSource Source { get; init; }
    public required DateOnly Date { get; init; }
    public required string Text { get; init; }
    public required AccountingEntryKind Kind { get; init; }
    public required int MoneyAccountId { get; init; }
    public int? ToMoneyAccountId { get; init; }
    public int? CategoryId { get; init; }

    /// <summary>In the direction of the category (negative = refund), or the amount of a transfer.</summary>
    public required int AmountCents { get; init; }

    public int? CounterpartyMemberId { get; init; }
    public string? CounterpartyName { get; init; }
    public required AccountingProposalStatus Status { get; init; }
    public int? EntryId { get; init; }

    /// <summary>Why it could not be booked by itself: an error code of <c>BackendErrorCodeLocale</c>.</summary>
    public string? Problem { get; init; }
}

public record SyncAccountingProposalsResult
{
    /// <summary>New suggestions.</summary>
    public required int Created { get; init; }

    /// <summary>Suggestions booked by themselves.</summary>
    public required int Booked { get; init; }

    /// <summary>Suggestions that wait for the treasurer.</summary>
    public required int Open { get; init; }

    /// <summary>Stripe could not be asked (no connection, not configured): its fees and payouts were left out this time.</summary>
    public required bool StripeFailed { get; init; }
}

/// <summary>Body of <c>POST Accounting/proposals/{id}/book</c>: the entry as the treasurer changed it, or nothing to book it as suggested.</summary>
public record BookAccountingProposalModel
{
    public SaveAccountingEntryModel? Entry { get; init; }
}

// ---------- bank ----------

/// <summary>Which column of a CSV export holds what (0-based), see <c>Bookennis.Domain.Accounting.CsvColumns</c>.</summary>
public record CsvColumnsDto
{
    public int SkipRows { get; init; }
    public string Delimiter { get; init; } = ";";
    public int BookingDate { get; init; }
    public int? ValueDate { get; init; }
    public int? Amount { get; init; }
    public int? Debit { get; init; }
    public int? Credit { get; init; }
    public int? DebitCreditMarker { get; init; }
    public int? CounterpartyName { get; init; }
    public int? CounterpartyIban { get; init; }
    public List<int> Reference { get; init; } = [];
}

/// <summary>What the upload of a bank statement did.</summary>
public record BankImportResultDto
{
    public required BankStatementFormat Format { get; init; }

    /// <summary>The CSV columns are not known: map them with <see cref="Preview"/> and upload again with the layout.</summary>
    public required bool NeedsCsvLayout { get; init; }

    /// <summary>The first rows of the file, split into cells.</summary>
    public List<List<string>> Preview { get; init; } = [];

    /// <summary>What was recognized (to start the mapping with).</summary>
    public CsvColumnsDto? Columns { get; init; }

    public int? ImportId { get; init; }
    public int Imported { get; init; }

    /// <summary>Lines that were imported before (overlapping files).</summary>
    public int Skipped { get; init; }

    /// <summary>The file is about another account (its IBAN differs from the one of the money account).</summary>
    public string? OtherIban { get; init; }
}

public record BankStatementImportDto
{
    public required int Id { get; init; }
    public required int MoneyAccountId { get; init; }
    public required string FileName { get; init; }
    public required BankStatementFormat Format { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public required DateTimeOffset ImportedAt { get; init; }
    public string? ImportedByName { get; init; }
    public required int ImportedCount { get; init; }
    public required int SkippedCount { get; init; }

    /// <summary>The balance the bank states at <see cref="ClosingDate"/>.</summary>
    public long? ClosingBalanceCents { get; init; }

    public DateOnly? ClosingDate { get; init; }

    /// <summary>The balance of the money account in Clubnetz at <see cref="ClosingDate"/>; <c>null</c> when no fiscal year contains the date.</summary>
    public long? BooksBalanceCents { get; init; }
}

public record BankTransactionDto
{
    public required int Id { get; init; }
    public required int MoneyAccountId { get; init; }
    public required DateOnly BookingDate { get; init; }
    public DateOnly? ValueDate { get; init; }

    /// <summary>Positive = money came in.</summary>
    public required int AmountCents { get; init; }

    public string? CounterpartyName { get; init; }
    public string? CounterpartyIban { get; init; }
    public required string Reference { get; init; }
    public required BankTransactionStatus Status { get; init; }
    public int? EntryId { get; init; }
    public int? EntryNumber { get; init; }

    /// <summary>Entries and suggestions with this amount around this date that are not matched yet.</summary>
    public List<BankMatchDto> Matches { get; init; } = [];

    /// <summary>The first rule that fits the line.</summary>
    public int? RuleId { get; init; }
}

public enum BankMatchKind
{
    Entry = 0,
    Proposal = 1,

    /// <summary>An open invoice of this amount (paid then).</summary>
    Invoice = 2,
}

public record BankMatchDto
{
    public required BankMatchKind Kind { get; init; }
    public required int Id { get; init; }

    /// <summary>The entry number (entries), or the invoice number in <see cref="Text"/>.</summary>
    public int? Number { get; init; }

    public required DateOnly Date { get; init; }
    public required string Text { get; init; }
}

public record GetBankTransactionsResult
{
    public List<BankTransactionDto> Transactions { get; init; } = [];
    public required int OpenCount { get; init; }
}

/// <summary>Body of <c>POST Accounting/bank-transactions/{id}/match</c>: an entry or a suggestion (booked then) for the line.</summary>
public record MatchBankTransactionModel
{
    public int? EntryId { get; init; }
    public int? ProposalId { get; init; }

    /// <summary>An open invoice: it is paid with an income entry on the account and day of the line.</summary>
    public int? InvoiceId { get; init; }
}

public record BookBankTransactionsByRulesResult
{
    public required int Booked { get; init; }
}

public record BankRuleDto
{
    public required int Id { get; init; }
    public string? MatchText { get; init; }
    public string? MatchIban { get; init; }
    public required BankRuleDirection Direction { get; init; }
    public required int CategoryId { get; init; }
    public int? CostCenterId { get; init; }
    public string? Text { get; init; }
    public int? MoneyAccountId { get; init; }
}

/// <summary>Body of <c>POST|PUT Accounting/bank-rules</c>.</summary>
public record SaveBankRuleModel
{
    public string? MatchText { get; init; }
    public string? MatchIban { get; init; }
    public BankRuleDirection Direction { get; init; }
    public int CategoryId { get; init; }
    public int? CostCenterId { get; init; }
    public string? Text { get; init; }
    public int? MoneyAccountId { get; init; }
}
