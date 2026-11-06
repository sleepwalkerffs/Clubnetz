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
}
