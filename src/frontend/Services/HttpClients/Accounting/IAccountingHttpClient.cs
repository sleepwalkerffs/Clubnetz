using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Accounting;

namespace Bookennis.Client.Services.HttpClients.Accounting;

public interface IAccountingHttpClient
{
    public Task<HttpResult<AccountingDto>> Get(CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingDto>> SetUp(SetUpAccountingModel model, CancellationToken cancellationToken = default);

    public Task<HttpResult<FiscalYearDto>> CreateFiscalYear(CancellationToken cancellationToken = default);
    public Task<HttpResult<FiscalYearDto>> SetOpeningBalances(int fiscalYearId, SetOpeningBalancesModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<FiscalYearDto>> CloseFiscalYear(int fiscalYearId, CancellationToken cancellationToken = default);
    public Task<HttpResult<FiscalYearDto>> ReopenFiscalYear(int fiscalYearId, CancellationToken cancellationToken = default);
    public Task<HttpResult<FiscalYearDto>> RecordAudit(int fiscalYearId, RecordAuditModel model, CancellationToken cancellationToken = default);

    public Task<HttpResult<AccountingSummaryDto>> GetSummary(int fiscalYearId, CancellationToken cancellationToken = default);
    public Task<HttpResult<IncomeStatementDto>> GetIncomeStatement(int fiscalYearId, CancellationToken cancellationToken = default);
    public Task<HttpResult<CostCenterReportDto>> GetCostCenterReport(int fiscalYearId, CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountSheetDto>> GetAccountSheet(int fiscalYearId, int moneyAccountId, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> Export(int fiscalYearId, CancellationToken cancellationToken = default);

    public Task<HttpResult<MoneyAccountDto>> SaveMoneyAccount(int? moneyAccountId, SaveMoneyAccountModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteMoneyAccount(int moneyAccountId, CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingCategoryDto>> SaveCategory(int? categoryId, SaveAccountingCategoryModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteCategory(int categoryId, CancellationToken cancellationToken = default);
    public Task<HttpResult<CostCenterDto>> SaveCostCenter(int? costCenterId, SaveCostCenterModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteCostCenter(int costCenterId, CancellationToken cancellationToken = default);

    public Task<HttpResult<GetAccountingEntriesResult>> GetEntries(int fiscalYearId, AccountingEntryFilter filter, CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingEntryDetailDto>> GetEntry(int entryId, CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingEntryDetailDto>> SaveEntry(int? entryId, SaveAccountingEntryModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingEntryDetailDto>> CancelEntry(int entryId, CancelAccountingEntryModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<List<AccountingMemberDto>>> GetMembers(string? search, CancellationToken cancellationToken = default);

    public Task<HttpResult<List<AccountingReceiptDto>>> GetInbox(CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingReceiptDto>> UploadReceipt(Stream content, string fileName, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteReceipt(int receiptId, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> GetReceiptFile(int receiptId, CancellationToken cancellationToken = default);

    public Task<HttpResult<AccountingAutomationDto>> GetAutomation(CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingAutomationDto>> SaveAutomation(SaveAccountingAutomationModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<List<AccountingProposalDto>>> GetProposals(CancellationToken cancellationToken = default);
    public Task<HttpResult<SyncAccountingProposalsResult>> SyncProposals(CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingEntryDetailDto>> BookProposal(int proposalId, BookAccountingProposalModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DismissProposal(int proposalId, CancellationToken cancellationToken = default);

    public Task<HttpResult<BankImportResultDto>> ImportBankStatement(int moneyAccountId, Stream content, string fileName, CsvColumnsDto? layout, CancellationToken cancellationToken = default);
    public Task<HttpResult<List<BankStatementImportDto>>> GetBankStatementImports(int? moneyAccountId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetBankTransactionsResult>> GetBankTransactions(int? moneyAccountId, BankTransactionStatus status, CancellationToken cancellationToken = default);
    public Task<HttpResult> MatchBankTransaction(int bankTransactionId, MatchBankTransactionModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<AccountingEntryDetailDto>> BookBankTransaction(int bankTransactionId, SaveAccountingEntryModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<BookBankTransactionsByRulesResult>> BookBankTransactionsByRules(int? moneyAccountId, CancellationToken cancellationToken = default);
    public Task<HttpResult> IgnoreBankTransaction(int bankTransactionId, CancellationToken cancellationToken = default);
    public Task<HttpResult> ReopenBankTransaction(int bankTransactionId, CancellationToken cancellationToken = default);
    public Task<HttpResult<List<BankRuleDto>>> GetBankRules(CancellationToken cancellationToken = default);
    public Task<HttpResult<BankRuleDto>> SaveBankRule(int? bankRuleId, SaveBankRuleModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteBankRule(int bankRuleId, CancellationToken cancellationToken = default);
}
