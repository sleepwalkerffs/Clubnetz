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
}
