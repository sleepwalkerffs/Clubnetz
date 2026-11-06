using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Accounting;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Accounting;

namespace Bookennis.Client.Services.Store.Accounting;

/// <summary>
/// The accounting of the club (Finances): treasurers and admins keep the books, auditors read them and record their audit.
/// Holds what the pages pick from (fiscal years, money accounts, categories, cost centers) and the data of the selected fiscal year.
/// </summary>
public interface IAccountingStore : ISemaphoreStore
{
    /// <summary>Raised whenever something was (re)loaded or saved.</summary>
    event Action? OnAccountingChanged;

    /// <summary><c>null</c> until it was loaded.</summary>
    AccountingDto? Basics { get; }

    /// <summary>The fiscal year the pages show; the newest open one by default.</summary>
    FiscalYearDto? SelectedYear { get; }

    AccountingSummaryDto? Summary { get; }
    GetAccountingEntriesResult? Entries { get; }
    AccountingEntryFilter Filter { get; }
    List<AccountingReceiptDto> Inbox { get; }
    IncomeStatementDto? IncomeStatement { get; }
    CostCenterReportDto? CostCenterReport { get; }
    AccountSheetDto? AccountSheet { get; }

    Task Load();
    Task SelectYear(int fiscalYearId);
    Task LoadSummary();

    /// <summary>The summary of any fiscal year, not kept in the store (e.g. to check what is open before closing it).</summary>
    Task<HttpResult<AccountingSummaryDto>> GetSummary(int fiscalYearId);
    Task LoadEntries(AccountingEntryFilter filter);
    Task LoadInbox();
    Task LoadReports();
    Task LoadAccountSheet(int moneyAccountId);

    Task<HttpResult<AccountingDto>> SetUp(SetUpAccountingModel model);
    Task<HttpResult<FiscalYearDto>> CreateFiscalYear();
    Task<HttpResult<FiscalYearDto>> SetOpeningBalances(int fiscalYearId, SetOpeningBalancesModel model);
    Task<HttpResult<FiscalYearDto>> CloseFiscalYear(int fiscalYearId);
    Task<HttpResult<FiscalYearDto>> ReopenFiscalYear(int fiscalYearId);
    Task<HttpResult<FiscalYearDto>> RecordAudit(int fiscalYearId, RecordAuditModel model);
    Task<HttpResult<FileDownload>> Export(int fiscalYearId);

    Task<HttpResult<MoneyAccountDto>> SaveMoneyAccount(int? moneyAccountId, SaveMoneyAccountModel model);
    Task<HttpResult> DeleteMoneyAccount(int moneyAccountId);
    Task<HttpResult<AccountingCategoryDto>> SaveCategory(int? categoryId, SaveAccountingCategoryModel model);
    Task<HttpResult> DeleteCategory(int categoryId);
    Task<HttpResult<CostCenterDto>> SaveCostCenter(int? costCenterId, SaveCostCenterModel model);
    Task<HttpResult> DeleteCostCenter(int costCenterId);

    Task<HttpResult<AccountingEntryDetailDto>> GetEntry(int entryId);
    Task<HttpResult<AccountingEntryDetailDto>> SaveEntry(int? entryId, SaveAccountingEntryModel model);
    Task<HttpResult<AccountingEntryDetailDto>> CancelEntry(int entryId, string? reason);
    Task<HttpResult<List<AccountingMemberDto>>> GetMembers(string? search, CancellationToken cancellationToken = default);

    Task<HttpResult<AccountingReceiptDto>> UploadReceipt(Stream content, string fileName);
    Task<HttpResult> DeleteReceipt(int receiptId);
    Task<HttpResult<FileDownload>> GetReceiptFile(int receiptId);

    // ---------- automatic entries and bank ----------

    AccountingAutomationDto? Automation { get; }

    /// <summary>The open suggestions.</summary>
    List<AccountingProposalDto> Proposals { get; }

    /// <summary>The bank lines of <see cref="BankAccountId"/> with <see cref="BankStatus"/>.</summary>
    GetBankTransactionsResult? BankTransactions { get; }

    int? BankAccountId { get; }
    BankTransactionStatus BankStatus { get; }
    List<BankStatementImportDto> BankImports { get; }
    List<BankRuleDto> BankRules { get; }

    Task LoadAutomation();
    Task<HttpResult<AccountingAutomationDto>> SaveAutomation(SaveAccountingAutomationModel model);
    Task LoadProposals();
    Task<HttpResult<SyncAccountingProposalsResult>> SyncProposals();
    Task<HttpResult<AccountingEntryDetailDto>> BookProposal(int proposalId, SaveAccountingEntryModel? changed);
    Task<HttpResult> DismissProposal(int proposalId);

    /// <summary>The bank lines, imports and rules of a money account (all with <c>null</c>).</summary>
    Task LoadBank(int? moneyAccountId, BankTransactionStatus status);

    Task<HttpResult<BankImportResultDto>> ImportBankStatement(int moneyAccountId, Stream content, string fileName, CsvColumnsDto? layout);
    Task<HttpResult> MatchBankTransaction(int bankTransactionId, MatchBankTransactionModel model);
    Task<HttpResult<AccountingEntryDetailDto>> BookBankTransaction(int bankTransactionId, SaveAccountingEntryModel model);
    Task<HttpResult<BookBankTransactionsByRulesResult>> BookBankTransactionsByRules();
    Task<HttpResult> IgnoreBankTransaction(int bankTransactionId);
    Task<HttpResult> ReopenBankTransaction(int bankTransactionId);
    Task<HttpResult<BankRuleDto>> SaveBankRule(int? bankRuleId, SaveBankRuleModel model);
    Task<HttpResult> DeleteBankRule(int bankRuleId);

    // ---------- invoices ----------

    InvoiceSettingsDto? InvoiceSettings { get; }
    GetInvoicesResult? Invoices { get; }

    /// <summary>The status the list shows; <c>null</c> = all.</summary>
    InvoiceStatus? InvoiceFilter { get; }

    Task LoadInvoices(InvoiceStatus? status);
    Task<HttpResult<InvoiceSettingsDto>> SaveInvoiceSettings(SaveInvoiceSettingsModel model);
    Task<HttpResult<InvoiceDetailDto>> GetInvoice(int invoiceId);
    Task<HttpResult<InvoiceDetailDto>> SaveInvoice(int? invoiceId, SaveInvoiceModel model);
    Task<HttpResult> DeleteInvoice(int invoiceId);

    /// <summary>issue, send, remind, pay or cancel; paying books an entry.</summary>
    Task<HttpResult<InvoiceDetailDto>> InvoiceAction(int invoiceId, string action, object? body);

    Task<HttpResult<FileDownload>> GetInvoicePdf(int invoiceId);
}
