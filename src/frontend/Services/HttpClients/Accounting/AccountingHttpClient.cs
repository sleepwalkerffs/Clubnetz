using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Accounting;

namespace Bookennis.Client.Services.HttpClients.Accounting;

/// <summary>The filters of the list of entries.</summary>
public record AccountingEntryFilter(int? MoneyAccountId = null, int? CategoryId = null, int? CostCenterId = null, string? Search = null, bool IncludeCancelled = false);

public class AccountingHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IAccountingHttpClient
{
    public async Task<HttpResult<AccountingDto>> Get(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<AccountingDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingDto>> SetUp(SetUpAccountingModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("setup", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FiscalYearDto>> CreateFiscalYear(CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync("fiscal-years", null, cancellationToken)).AsHttpResult<FiscalYearDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FiscalYearDto>> SetOpeningBalances(int fiscalYearId, SetOpeningBalancesModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"fiscal-years/{fiscalYearId}/opening-balances", model, jsonOptions, cancellationToken)).AsHttpResult<FiscalYearDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FiscalYearDto>> CloseFiscalYear(int fiscalYearId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"fiscal-years/{fiscalYearId}/close", null, cancellationToken)).AsHttpResult<FiscalYearDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FiscalYearDto>> ReopenFiscalYear(int fiscalYearId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"fiscal-years/{fiscalYearId}/reopen", null, cancellationToken)).AsHttpResult<FiscalYearDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FiscalYearDto>> RecordAudit(int fiscalYearId, RecordAuditModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"fiscal-years/{fiscalYearId}/audit", model, jsonOptions, cancellationToken)).AsHttpResult<FiscalYearDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingSummaryDto>> GetSummary(int fiscalYearId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"fiscal-years/{fiscalYearId}/summary", cancellationToken)).AsHttpResult<AccountingSummaryDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<IncomeStatementDto>> GetIncomeStatement(int fiscalYearId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"fiscal-years/{fiscalYearId}/income-statement", cancellationToken)).AsHttpResult<IncomeStatementDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CostCenterReportDto>> GetCostCenterReport(int fiscalYearId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"fiscal-years/{fiscalYearId}/cost-centers", cancellationToken)).AsHttpResult<CostCenterReportDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountSheetDto>> GetAccountSheet(int fiscalYearId, int moneyAccountId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"fiscal-years/{fiscalYearId}/money-accounts/{moneyAccountId}/sheet", cancellationToken)).AsHttpResult<AccountSheetDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FileDownload>> Export(int fiscalYearId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"fiscal-years/{fiscalYearId}/export", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<MoneyAccountDto>> SaveMoneyAccount(int? moneyAccountId, SaveMoneyAccountModel model, CancellationToken cancellationToken = default)
        => await (moneyAccountId is { } id
            ? await httpClient.PutAsJsonAsync($"money-accounts/{id}", model, jsonOptions, cancellationToken)
            : await httpClient.PostAsJsonAsync("money-accounts", model, jsonOptions, cancellationToken)).AsHttpResult<MoneyAccountDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteMoneyAccount(int moneyAccountId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"money-accounts/{moneyAccountId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingCategoryDto>> SaveCategory(int? categoryId, SaveAccountingCategoryModel model, CancellationToken cancellationToken = default)
        => await (categoryId is { } id
            ? await httpClient.PutAsJsonAsync($"categories/{id}", model, jsonOptions, cancellationToken)
            : await httpClient.PostAsJsonAsync("categories", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingCategoryDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteCategory(int categoryId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"categories/{categoryId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<CostCenterDto>> SaveCostCenter(int? costCenterId, SaveCostCenterModel model, CancellationToken cancellationToken = default)
        => await (costCenterId is { } id
            ? await httpClient.PutAsJsonAsync($"cost-centers/{id}", model, jsonOptions, cancellationToken)
            : await httpClient.PostAsJsonAsync("cost-centers", model, jsonOptions, cancellationToken)).AsHttpResult<CostCenterDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteCostCenter(int costCenterId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"cost-centers/{costCenterId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetAccountingEntriesResult>> GetEntries(int fiscalYearId, AccountingEntryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (filter.MoneyAccountId is { } accountId)
            query.Add($"moneyAccountId={accountId.ToString(CultureInfo.InvariantCulture)}");
        if (filter.CategoryId is { } categoryId)
            query.Add($"categoryId={categoryId.ToString(CultureInfo.InvariantCulture)}");
        if (filter.CostCenterId is { } costCenterId)
            query.Add($"costCenterId={costCenterId.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(filter.Search))
            query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        if (filter.IncludeCancelled)
            query.Add("includeCancelled=true");

        var url = $"fiscal-years/{fiscalYearId}/entries" + (query.Count == 0 ? "" : "?" + string.Join("&", query));
        return await (await httpClient.GetAsync(url, cancellationToken)).AsHttpResult<GetAccountingEntriesResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<AccountingEntryDetailDto>> GetEntry(int entryId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"entries/{entryId}", cancellationToken)).AsHttpResult<AccountingEntryDetailDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingEntryDetailDto>> SaveEntry(int? entryId, SaveAccountingEntryModel model, CancellationToken cancellationToken = default)
        => await (entryId is { } id
            ? await httpClient.PutAsJsonAsync($"entries/{id}", model, jsonOptions, cancellationToken)
            : await httpClient.PostAsJsonAsync("entries", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingEntryDetailDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingEntryDetailDto>> CancelEntry(int entryId, CancelAccountingEntryModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"entries/{entryId}/cancel", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingEntryDetailDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<AccountingMemberDto>>> GetMembers(string? search, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(string.IsNullOrWhiteSpace(search) ? "members" : $"members?search={Uri.EscapeDataString(search)}", cancellationToken))
            .AsHttpResult<List<AccountingMemberDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<AccountingReceiptDto>>> GetInbox(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("receipts", cancellationToken)).AsHttpResult<List<AccountingReceiptDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingReceiptDto>> UploadReceipt(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        // The backend derives the content type from the file extension
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, "file", fileName);
        return await (await httpClient.PostAsync("receipts", form, cancellationToken)).AsHttpResult<AccountingReceiptDto>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> DeleteReceipt(int receiptId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"receipts/{receiptId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<FileDownload>> GetReceiptFile(int receiptId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"receipts/{receiptId}/file", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);

    // ---------- automatic entries ----------

    public async Task<HttpResult<AccountingAutomationDto>> GetAutomation(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("automation", cancellationToken)).AsHttpResult<AccountingAutomationDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingAutomationDto>> SaveAutomation(SaveAccountingAutomationModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("automation", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingAutomationDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<AccountingProposalDto>>> GetProposals(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("proposals", cancellationToken)).AsHttpResult<List<AccountingProposalDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<SyncAccountingProposalsResult>> SyncProposals(CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync("proposals/sync", null, cancellationToken)).AsHttpResult<SyncAccountingProposalsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingEntryDetailDto>> BookProposal(int proposalId, BookAccountingProposalModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"proposals/{proposalId}/book", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingEntryDetailDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DismissProposal(int proposalId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"proposals/{proposalId}/dismiss", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // ---------- bank ----------

    public async Task<HttpResult<BankImportResultDto>> ImportBankStatement(int moneyAccountId, Stream content, string fileName, CsvColumnsDto? layout, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, "file", fileName);
        form.Add(new StringContent(moneyAccountId.ToString(CultureInfo.InvariantCulture)), "moneyAccountId");
        if (layout is not null)
            form.Add(new StringContent(JsonSerializer.Serialize(layout, jsonOptions)), "layout");
        return await (await httpClient.PostAsync("bank-statements", form, cancellationToken)).AsHttpResult<BankImportResultDto>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<List<BankStatementImportDto>>> GetBankStatementImports(int? moneyAccountId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(moneyAccountId is { } id ? $"bank-statements?moneyAccountId={id.ToString(CultureInfo.InvariantCulture)}" : "bank-statements", cancellationToken))
            .AsHttpResult<List<BankStatementImportDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetBankTransactionsResult>> GetBankTransactions(int? moneyAccountId, BankTransactionStatus status, CancellationToken cancellationToken = default)
    {
        var url = $"bank-transactions?status={status}" + (moneyAccountId is { } id ? $"&moneyAccountId={id.ToString(CultureInfo.InvariantCulture)}" : "");
        return await (await httpClient.GetAsync(url, cancellationToken)).AsHttpResult<GetBankTransactionsResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> MatchBankTransaction(int bankTransactionId, MatchBankTransactionModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"bank-transactions/{bankTransactionId}/match", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<AccountingEntryDetailDto>> BookBankTransaction(int bankTransactionId, SaveAccountingEntryModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"bank-transactions/{bankTransactionId}/book", model, jsonOptions, cancellationToken)).AsHttpResult<AccountingEntryDetailDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<BookBankTransactionsByRulesResult>> BookBankTransactionsByRules(int? moneyAccountId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync(moneyAccountId is { } id ? $"bank-transactions/book-by-rules?moneyAccountId={id.ToString(CultureInfo.InvariantCulture)}" : "bank-transactions/book-by-rules", null, cancellationToken))
            .AsHttpResult<BookBankTransactionsByRulesResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> IgnoreBankTransaction(int bankTransactionId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"bank-transactions/{bankTransactionId}/ignore", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ReopenBankTransaction(int bankTransactionId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"bank-transactions/{bankTransactionId}/reopen", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<BankRuleDto>>> GetBankRules(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("bank-rules", cancellationToken)).AsHttpResult<List<BankRuleDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<BankRuleDto>> SaveBankRule(int? bankRuleId, SaveBankRuleModel model, CancellationToken cancellationToken = default)
        => await (bankRuleId is { } id
            ? await httpClient.PutAsJsonAsync($"bank-rules/{id}", model, jsonOptions, cancellationToken)
            : await httpClient.PostAsJsonAsync("bank-rules", model, jsonOptions, cancellationToken)).AsHttpResult<BankRuleDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteBankRule(int bankRuleId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"bank-rules/{bankRuleId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
