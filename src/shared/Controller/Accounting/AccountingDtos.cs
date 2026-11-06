namespace Bookennis.Shared.Controller.Accounting;

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.FiscalYearStatus</c>.</summary>
public enum FiscalYearStatus
{
    Open = 0,
    Closed = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.FiscalYearAuditStatus</c>.</summary>
public enum FiscalYearAuditStatus
{
    NotAudited = 0,
    Audited = 1,
    AuditedWithRemarks = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.MoneyAccountKind</c>.</summary>
public enum MoneyAccountKind
{
    Bank = 0,
    Cash = 1,
    PaymentProvider = 2,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingCategoryKind</c>.</summary>
public enum AccountingCategoryKind
{
    Income = 0,
    Expense = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.TaxSphere</c>.</summary>
public enum TaxSphere
{
    NonProfit = 0,
    AssetManagement = 1,
    AuxiliaryOperation = 2,
    CommercialOperation = 3,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingEntryKind</c>.</summary>
public enum AccountingEntryKind
{
    Booking = 0,
    Transfer = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingEntryStatus</c>.</summary>
public enum AccountingEntryStatus
{
    Active = 0,
    Cancelled = 1,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Accounting.AccountingEntryChangeKind</c>.</summary>
public enum AccountingEntryChangeKind
{
    Created = 0,
    Changed = 1,
    Cancelled = 2,
}

// ---------- basics ----------

/// <summary>Answer of <c>GET Accounting</c>: whether the accounting is set up, and everything the pages pick from.</summary>
public record AccountingDto
{
    public required bool IsSetUp { get; init; }

    /// <summary>1-12, <c>null</c> while not set up.</summary>
    public int? FiscalYearStartMonth { get; init; }

    /// <summary>Newest first.</summary>
    public List<FiscalYearDto> FiscalYears { get; init; } = [];

    public List<MoneyAccountDto> MoneyAccounts { get; init; } = [];
    public List<AccountingCategoryDto> Categories { get; init; } = [];
    public List<CostCenterDto> CostCenters { get; init; } = [];

    /// <summary>Treasurer or admin: may change everything.</summary>
    public required bool CanManage { get; init; }

    /// <summary>Auditor: may record the audit of a closed fiscal year.</summary>
    public required bool CanAudit { get; init; }
}

public record FiscalYearDto
{
    public required int Id { get; init; }

    /// <summary>"2026", or "2025/26" for a year that does not start in January.</summary>
    public required string Label { get; init; }

    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required FiscalYearStatus Status { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public string? ClosedByName { get; init; }

    /// <summary>The year before carries its closing balances into this year (see <see cref="OpeningBalanceDto.IsCarried"/>).</summary>
    public required bool OpeningBalancesCarried { get; init; }

    public List<OpeningBalanceDto> OpeningBalances { get; init; } = [];
    public required FiscalYearAuditStatus AuditStatus { get; init; }
    public DateTimeOffset? AuditedAt { get; init; }
    public string? AuditedByName { get; init; }
    public string? AuditComment { get; init; }
}

public record OpeningBalanceDto
{
    public required int MoneyAccountId { get; init; }
    public required long AmountCents { get; init; }

    /// <summary>The closing balance of the year before: can't be changed by hand.</summary>
    public required bool IsCarried { get; init; }
}

public record MoneyAccountDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required MoneyAccountKind Kind { get; init; }

    /// <summary>Without spaces.</summary>
    public string? Iban { get; init; }

    public required bool IsArchived { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>Entries or opening balances use it: it can only be archived, not deleted.</summary>
    public required bool IsUsed { get; init; }
}

public record AccountingCategoryDto
{
    public required int Id { get; init; }
    public required AccountingCategoryKind Kind { get; init; }
    public string? Number { get; init; }
    public required string Name { get; init; }
    public required TaxSphere TaxSphere { get; init; }
    public required bool IsArchived { get; init; }

    /// <summary>Entries use it: it can only be archived, not deleted, and it stays income or expense.</summary>
    public required bool IsUsed { get; init; }
}

public record CostCenterDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required bool IsArchived { get; init; }
    public required bool IsUsed { get; init; }
}

/// <summary>Body of <c>POST Accounting/setup</c>. Creates the first fiscal year, two money accounts and the categories of the template.</summary>
public record SetUpAccountingModel
{
    /// <summary>1 = January (the fiscal year is the calendar year).</summary>
    public int FiscalYearStartMonth { get; init; } = 1;

    /// <summary>The calendar year the first fiscal year starts in.</summary>
    public int FirstYear { get; init; }
}

/// <summary>Body of <c>POST|PUT Accounting/money-accounts</c>.</summary>
public record SaveMoneyAccountModel
{
    public string? Name { get; init; }
    public MoneyAccountKind Kind { get; init; }
    public string? Iban { get; init; }
    public int SortOrder { get; init; }
    public bool IsArchived { get; init; }
}

/// <summary>Body of <c>POST|PUT Accounting/categories</c>.</summary>
public record SaveAccountingCategoryModel
{
    public AccountingCategoryKind Kind { get; init; }
    public string? Number { get; init; }
    public string? Name { get; init; }
    public TaxSphere TaxSphere { get; init; }
    public bool IsArchived { get; init; }
}

/// <summary>Body of <c>POST|PUT Accounting/cost-centers</c>.</summary>
public record SaveCostCenterModel
{
    public string? Name { get; init; }
    public bool IsArchived { get; init; }
}

/// <summary>Body of <c>PUT Accounting/fiscal-years/{id}/opening-balances</c>. Money accounts that are left out keep their balance.</summary>
public record SetOpeningBalancesModel
{
    public List<OpeningBalanceModel> Balances { get; init; } = [];
}

public record OpeningBalanceModel
{
    public int MoneyAccountId { get; init; }
    public int AmountCents { get; init; }
}

/// <summary>Body of <c>POST Accounting/fiscal-years/{id}/audit</c>.</summary>
public record RecordAuditModel
{
    public FiscalYearAuditStatus Status { get; init; }
    public string? Comment { get; init; }
}

// ---------- entries ----------

public record AccountingEntryLineDto
{
    public required int CategoryId { get; init; }
    public int? CostCenterId { get; init; }

    /// <summary>In the direction of the category (an income adds, an expense takes away); negative for a refund.</summary>
    public required int AmountCents { get; init; }
}

public record AccountingEntryDto
{
    public required int Id { get; init; }
    public required int FiscalYearId { get; init; }
    public required int Number { get; init; }
    public required DateOnly Date { get; init; }
    public required string Text { get; init; }
    public required AccountingEntryKind Kind { get; init; }
    public required int MoneyAccountId { get; init; }
    public int? ToMoneyAccountId { get; init; }
    public int? TransferAmountCents { get; init; }

    /// <summary>What it does to <see cref="MoneyAccountId"/>: income minus expenses; for a transfer the amount moved (positive).</summary>
    public required long AmountCents { get; init; }

    public int? CounterpartyMemberId { get; init; }
    public string? CounterpartyName { get; init; }
    public required AccountingEntryStatus Status { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public List<AccountingEntryLineDto> Lines { get; init; } = [];
    public required int ReceiptCount { get; init; }
}

/// <summary>An entry with its receipts and its history.</summary>
public record AccountingEntryDetailDto : AccountingEntryDto
{
    public List<AccountingReceiptDto> Receipts { get; init; } = [];

    /// <summary>Oldest first.</summary>
    public List<AccountingEntryChangeDto> History { get; init; } = [];
}

/// <summary>One version of an entry: who saved it when and what it looked like afterwards.</summary>
public record AccountingEntryChangeDto
{
    public required AccountingEntryChangeKind Kind { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? ByName { get; init; }
    public required DateOnly Date { get; init; }
    public required string Text { get; init; }
    public required AccountingEntryKind EntryKind { get; init; }
    public required int MoneyAccountId { get; init; }
    public int? ToMoneyAccountId { get; init; }
    public int? TransferAmountCents { get; init; }
    public string? CounterpartyName { get; init; }
    public List<AccountingEntryLineDto> Lines { get; init; } = [];
    public string? CancelReason { get; init; }
    public List<int> ReceiptIds { get; init; } = [];
}

public record GetAccountingEntriesResult
{
    /// <summary>Newest first (date, then number).</summary>
    public List<AccountingEntryDto> Entries { get; init; } = [];

    /// <summary>The sums of the lines of the active entries in the list (cancelled ones and transfers don't count).</summary>
    public required long IncomeCents { get; init; }

    public required long ExpenseCents { get; init; }
}

/// <summary>Body of <c>POST|PUT Accounting/entries</c>.</summary>
public record SaveAccountingEntryModel
{
    public DateOnly Date { get; init; }
    public string? Text { get; init; }
    public AccountingEntryKind Kind { get; init; }
    public int MoneyAccountId { get; init; }
    public int? ToMoneyAccountId { get; init; }
    public int? TransferAmountCents { get; init; }
    public List<SaveAccountingEntryLineModel> Lines { get; init; } = [];

    /// <summary>A member of the club who paid or was paid; <see cref="CounterpartyName"/> defaults to the member's name.</summary>
    public int? CounterpartyMemberId { get; init; }

    public string? CounterpartyName { get; init; }

    /// <summary>All receipts of the entry: receipts from the inbox are attached, receipts left out go back to the inbox.</summary>
    public List<int> ReceiptIds { get; init; } = [];
}

public record SaveAccountingEntryLineModel
{
    public int CategoryId { get; init; }
    public int? CostCenterId { get; init; }
    public int AmountCents { get; init; }
}

/// <summary>Body of <c>POST Accounting/entries/{id}/cancel</c>.</summary>
public record CancelAccountingEntryModel
{
    public string? Reason { get; init; }
}

public record AccountingReceiptDto
{
    public required int Id { get; init; }
    public int? EntryId { get; init; }

    /// <summary>The number of the entry, the receipt is filed under it.</summary>
    public int? EntryNumber { get; init; }

    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required int Size { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public string? UploadedByName { get; init; }
}

/// <summary>A member of the club the treasurer picks as the counterparty: name only.</summary>
public record AccountingMemberDto
{
    public required int MemberId { get; init; }
    public required string Name { get; init; }
}

// ---------- reports ----------

/// <summary>The overview of a fiscal year: the money accounts (Vermögensübersicht) and the totals.</summary>
public record AccountingSummaryDto
{
    public required int FiscalYearId { get; init; }
    public List<MoneyAccountBalanceDto> Accounts { get; init; } = [];
    public required long OpeningCents { get; init; }
    public required long IncomeCents { get; init; }
    public required long ExpenseCents { get; init; }
    public required long ClosingCents { get; init; }
    public required int EntryCount { get; init; }
    public required int CancelledCount { get; init; }

    /// <summary>Active income/expense entries without any receipt.</summary>
    public required int EntriesWithoutReceipt { get; init; }

    /// <summary>Receipts waiting in the inbox (all years).</summary>
    public required int ReceiptsInInbox { get; init; }

    /// <summary>Suggested entries of this year still waiting to be booked or dismissed.</summary>
    public int OpenProposals { get; init; }

    /// <summary>Imported bank lines of this year neither matched nor ignored.</summary>
    public int OpenBankLines { get; init; }
}

public record MoneyAccountBalanceDto
{
    public required int MoneyAccountId { get; init; }
    public required long OpeningCents { get; init; }
    public required long IncomeCents { get; init; }
    public required long ExpenseCents { get; init; }
    public required long TransfersInCents { get; init; }
    public required long TransfersOutCents { get; init; }
    public required long ClosingCents { get; init; }
}

/// <summary>The income/expense statement (Einnahmen-Ausgaben-Rechnung) by tax sphere and category, with the year before.</summary>
public record IncomeStatementDto
{
    public required int FiscalYearId { get; init; }
    public int? PreviousFiscalYearId { get; init; }
    public List<IncomeStatementSphereDto> Spheres { get; init; } = [];
    public required long IncomeCents { get; init; }
    public required long ExpenseCents { get; init; }
    public long? PreviousIncomeCents { get; init; }
    public long? PreviousExpenseCents { get; init; }
}

public record IncomeStatementSphereDto
{
    public required TaxSphere TaxSphere { get; init; }
    public required long IncomeCents { get; init; }
    public required long ExpenseCents { get; init; }
    public long? PreviousIncomeCents { get; init; }
    public long? PreviousExpenseCents { get; init; }

    /// <summary>Income first, then expenses, each by number and name. Only categories with an amount in one of the two years.</summary>
    public List<IncomeStatementCategoryDto> Categories { get; init; } = [];
}

public record IncomeStatementCategoryDto
{
    public required int CategoryId { get; init; }
    public required AccountingCategoryKind Kind { get; init; }
    public required long AmountCents { get; init; }
    public long? PreviousAmountCents { get; init; }
}

/// <summary>Income and expenses by cost center; the lines without one are in the row with <c>CostCenterId = null</c>.</summary>
public record CostCenterReportDto
{
    public required int FiscalYearId { get; init; }
    public List<CostCenterReportRowDto> Rows { get; init; } = [];
}

public record CostCenterReportRowDto
{
    public int? CostCenterId { get; init; }
    public required long IncomeCents { get; init; }
    public required long ExpenseCents { get; init; }
    public List<IncomeStatementCategoryDto> Categories { get; init; } = [];
}

/// <summary>The cash book / account sheet of one money account in a fiscal year, oldest first, with the running balance.</summary>
public record AccountSheetDto
{
    public required int FiscalYearId { get; init; }
    public required int MoneyAccountId { get; init; }
    public required long OpeningCents { get; init; }
    public required long ClosingCents { get; init; }
    public List<AccountSheetRowDto> Rows { get; init; } = [];
}

public record AccountSheetRowDto
{
    public required int EntryId { get; init; }
    public required int Number { get; init; }
    public required DateOnly Date { get; init; }
    public required string Text { get; init; }
    public string? CounterpartyName { get; init; }

    /// <summary>What the entry did to the account (in positive, out negative).</summary>
    public required long AmountCents { get; init; }

    public required long BalanceCents { get; init; }
}
