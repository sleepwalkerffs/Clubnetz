using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Accounting;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Accounting;

namespace Bookennis.Client.Services.Store.Accounting;

public class AccountingStore(IAccountingHttpClient httpClient) : SemaphoreStore, IAccountingStore
{
    private int? selectedYearId;

    public event Action? OnAccountingChanged;

    public AccountingDto? Basics { get; private set; }
    public FiscalYearDto? SelectedYear => Basics?.FiscalYears.FirstOrDefault(y => y.Id == selectedYearId);
    public AccountingSummaryDto? Summary { get; private set; }
    public GetAccountingEntriesResult? Entries { get; private set; }
    public AccountingEntryFilter Filter { get; private set; } = new();
    public List<AccountingReceiptDto> Inbox { get; private set; } = [];
    public IncomeStatementDto? IncomeStatement { get; private set; }
    public CostCenterReportDto? CostCenterReport { get; private set; }
    public AccountSheetDto? AccountSheet { get; private set; }

    public Task Load() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.Get(cancellationToken);
        if (response is { Success: true, Dto: not null })
            SetBasics(response.Dto);
    }, nameof(Load));

    public async Task SelectYear(int fiscalYearId)
    {
        if (selectedYearId == fiscalYearId)
            return;

        selectedYearId = fiscalYearId;
        Summary = null;
        Entries = null;
        IncomeStatement = null;
        CostCenterReport = null;
        AccountSheet = null;
        OnAccountingChanged?.Invoke();
        await LoadSummary();
    }

    public Task LoadSummary() => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (selectedYearId is not { } yearId)
            return;

        var response = await httpClient.GetSummary(yearId, cancellationToken);
        if (response is { Success: true, Dto: not null } && response.Dto.FiscalYearId == selectedYearId)
        {
            Summary = response.Dto;
            OnAccountingChanged?.Invoke();
        }
    }, nameof(LoadSummary));

    public Task LoadEntries(AccountingEntryFilter filter) => RunInLoadingContextAsync(async cancellationToken =>
    {
        Filter = filter;
        if (selectedYearId is not { } yearId)
            return;

        var response = await httpClient.GetEntries(yearId, filter, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Entries = response.Dto;
            OnAccountingChanged?.Invoke();
        }
    }, nameof(LoadEntries));

    public Task LoadInbox() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetInbox(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Inbox = response.Dto;
            OnAccountingChanged?.Invoke();
        }
    }, nameof(LoadInbox));

    public Task LoadReports() => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (selectedYearId is not { } yearId)
            return;

        var statement = httpClient.GetIncomeStatement(yearId, cancellationToken);
        var costCenters = httpClient.GetCostCenterReport(yearId, cancellationToken);
        if (await statement is { Success: true, Dto: not null } s)
            IncomeStatement = s.Dto;
        if (await costCenters is { Success: true, Dto: not null } c)
            CostCenterReport = c.Dto;
        OnAccountingChanged?.Invoke();
    }, nameof(LoadReports));

    public Task LoadAccountSheet(int moneyAccountId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (selectedYearId is not { } yearId)
            return;

        var response = await httpClient.GetAccountSheet(yearId, moneyAccountId, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            AccountSheet = response.Dto;
            OnAccountingChanged?.Invoke();
        }
    }, nameof(LoadAccountSheet));

    public async Task<HttpResult<AccountingDto>> SetUp(SetUpAccountingModel model)
        => (HttpResult<AccountingDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SetUp(model, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                SetBasics(result.Dto);
                await LoadSummary();
            }

            return result;
        }, nameof(SetUp));

    public Task<HttpResult<FiscalYearDto>> CreateFiscalYear() => SaveYear(token => httpClient.CreateFiscalYear(token), nameof(CreateFiscalYear));

    public Task<HttpResult<FiscalYearDto>> SetOpeningBalances(int fiscalYearId, SetOpeningBalancesModel model)
        => SaveYear(token => httpClient.SetOpeningBalances(fiscalYearId, model, token), nameof(SetOpeningBalances));

    public Task<HttpResult<FiscalYearDto>> CloseFiscalYear(int fiscalYearId) => SaveYear(token => httpClient.CloseFiscalYear(fiscalYearId, token), nameof(CloseFiscalYear));

    public Task<HttpResult<FiscalYearDto>> ReopenFiscalYear(int fiscalYearId) => SaveYear(token => httpClient.ReopenFiscalYear(fiscalYearId, token), nameof(ReopenFiscalYear));

    public Task<HttpResult<FiscalYearDto>> RecordAudit(int fiscalYearId, RecordAuditModel model) => SaveYear(token => httpClient.RecordAudit(fiscalYearId, model, token), nameof(RecordAudit));

    public async Task<HttpResult<FileDownload>> Export(int fiscalYearId)
        => (HttpResult<FileDownload>)await RunInSavingContextAsync(async cancellationToken => await httpClient.Export(fiscalYearId, cancellationToken), nameof(Export));

    public Task<HttpResult<MoneyAccountDto>> SaveMoneyAccount(int? moneyAccountId, SaveMoneyAccountModel model)
        => SaveAndReload(token => httpClient.SaveMoneyAccount(moneyAccountId, model, token), nameof(SaveMoneyAccount));

    public Task<HttpResult> DeleteMoneyAccount(int moneyAccountId) => DeleteAndReload(token => httpClient.DeleteMoneyAccount(moneyAccountId, token), nameof(DeleteMoneyAccount));

    public Task<HttpResult<AccountingCategoryDto>> SaveCategory(int? categoryId, SaveAccountingCategoryModel model)
        => SaveAndReload(token => httpClient.SaveCategory(categoryId, model, token), nameof(SaveCategory));

    public Task<HttpResult> DeleteCategory(int categoryId) => DeleteAndReload(token => httpClient.DeleteCategory(categoryId, token), nameof(DeleteCategory));

    public Task<HttpResult<CostCenterDto>> SaveCostCenter(int? costCenterId, SaveCostCenterModel model)
        => SaveAndReload(token => httpClient.SaveCostCenter(costCenterId, model, token), nameof(SaveCostCenter));

    public Task<HttpResult> DeleteCostCenter(int costCenterId) => DeleteAndReload(token => httpClient.DeleteCostCenter(costCenterId, token), nameof(DeleteCostCenter));

    public Task<HttpResult<AccountingEntryDetailDto>> GetEntry(int entryId) => httpClient.GetEntry(entryId);

    public async Task<HttpResult<AccountingEntryDetailDto>> SaveEntry(int? entryId, SaveAccountingEntryModel model)
        => (HttpResult<AccountingEntryDetailDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SaveEntry(entryId, model, cancellationToken);
            if (result.Success)
                await AfterEntryChanged();

            return result;
        }, nameof(SaveEntry));

    public async Task<HttpResult<AccountingEntryDetailDto>> CancelEntry(int entryId, string? reason)
        => (HttpResult<AccountingEntryDetailDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.CancelEntry(entryId, new CancelAccountingEntryModel { Reason = reason }, cancellationToken);
            if (result.Success)
                await AfterEntryChanged();

            return result;
        }, nameof(CancelEntry));

    public Task<HttpResult<List<AccountingMemberDto>>> GetMembers(string? search, CancellationToken cancellationToken = default) => httpClient.GetMembers(search, cancellationToken);

    public async Task<HttpResult<AccountingReceiptDto>> UploadReceipt(Stream content, string fileName)
        => (HttpResult<AccountingReceiptDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UploadReceipt(content, fileName, cancellationToken);
            if (result.Success)
                await LoadInbox();

            return result;
        }, nameof(UploadReceipt));

    public Task<HttpResult> DeleteReceipt(int receiptId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeleteReceipt(receiptId, cancellationToken);
            if (result.Success)
                await LoadInbox();

            return result;
        }, nameof(DeleteReceipt));

    public Task<HttpResult<FileDownload>> GetReceiptFile(int receiptId) => httpClient.GetReceiptFile(receiptId);

    // ---------- automatic entries ----------

    public AccountingAutomationDto? Automation { get; private set; }
    public List<AccountingProposalDto> Proposals { get; private set; } = [];

    public Task LoadAutomation() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetAutomation(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Automation = response.Dto;
            OnAccountingChanged?.Invoke();
        }
    }, nameof(LoadAutomation));

    public async Task<HttpResult<AccountingAutomationDto>> SaveAutomation(SaveAccountingAutomationModel model)
        => (HttpResult<AccountingAutomationDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SaveAutomation(model, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Automation = result.Dto;
                OnAccountingChanged?.Invoke();
            }

            return result;
        }, nameof(SaveAutomation));

    public Task LoadProposals() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetProposals(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Proposals = response.Dto;
            OnAccountingChanged?.Invoke();
        }
    }, nameof(LoadProposals));

    public async Task<HttpResult<SyncAccountingProposalsResult>> SyncProposals()
        => (HttpResult<SyncAccountingProposalsResult>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SyncProposals(cancellationToken);
            if (result.Success)
                await AfterReconciled();

            return result;
        }, nameof(SyncProposals));

    public async Task<HttpResult<AccountingEntryDetailDto>> BookProposal(int proposalId, SaveAccountingEntryModel? changed)
        => (HttpResult<AccountingEntryDetailDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.BookProposal(proposalId, new BookAccountingProposalModel { Entry = changed }, cancellationToken);
            if (result.Success)
                await AfterReconciled();

            return result;
        }, nameof(BookProposal));

    public Task<HttpResult> DismissProposal(int proposalId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DismissProposal(proposalId, cancellationToken);
            if (result.Success)
                await Task.WhenAll(LoadProposals(), ReloadBank());

            return result;
        }, nameof(DismissProposal));

    // ---------- bank ----------

    public GetBankTransactionsResult? BankTransactions { get; private set; }
    public int? BankAccountId { get; private set; }
    public BankTransactionStatus BankStatus { get; private set; }
    public List<BankStatementImportDto> BankImports { get; private set; } = [];
    public List<BankRuleDto> BankRules { get; private set; } = [];

    public Task LoadBank(int? moneyAccountId, BankTransactionStatus status) => RunInLoadingContextAsync(async cancellationToken =>
    {
        BankAccountId = moneyAccountId;
        BankStatus = status;
        var transactions = httpClient.GetBankTransactions(moneyAccountId, status, cancellationToken);
        var imports = httpClient.GetBankStatementImports(moneyAccountId, cancellationToken);
        var rules = httpClient.GetBankRules(cancellationToken);
        // A failed load shows nothing rather than what was there before (e.g. of another club)
        var transactionResult = await transactions;
        if (BankAccountId == moneyAccountId && BankStatus == status)
            BankTransactions = transactionResult is { Success: true, Dto: not null } t ? t.Dto : null;
        BankImports = await imports is { Success: true, Dto: not null } i ? i.Dto : [];
        BankRules = await rules is { Success: true, Dto: not null } r ? r.Dto : [];
        OnAccountingChanged?.Invoke();
    }, nameof(LoadBank));

    public async Task<HttpResult<BankImportResultDto>> ImportBankStatement(int moneyAccountId, Stream content, string fileName, CsvColumnsDto? layout)
        => (HttpResult<BankImportResultDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.ImportBankStatement(moneyAccountId, content, fileName, layout, cancellationToken);
            if (result is { Success: true, Dto.NeedsCsvLayout: false })
                await ReloadBank();

            return result;
        }, nameof(ImportBankStatement));

    public Task<HttpResult> MatchBankTransaction(int bankTransactionId, MatchBankTransactionModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.MatchBankTransaction(bankTransactionId, model, cancellationToken);
            if (result.Success)
                await AfterReconciled();

            return result;
        }, nameof(MatchBankTransaction));

    public async Task<HttpResult<AccountingEntryDetailDto>> BookBankTransaction(int bankTransactionId, SaveAccountingEntryModel model)
        => (HttpResult<AccountingEntryDetailDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.BookBankTransaction(bankTransactionId, model, cancellationToken);
            if (result.Success)
                await AfterReconciled();

            return result;
        }, nameof(BookBankTransaction));

    public async Task<HttpResult<BookBankTransactionsByRulesResult>> BookBankTransactionsByRules()
        => (HttpResult<BookBankTransactionsByRulesResult>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.BookBankTransactionsByRules(BankAccountId, cancellationToken);
            if (result.Success)
                await AfterReconciled();

            return result;
        }, nameof(BookBankTransactionsByRules));

    public Task<HttpResult> IgnoreBankTransaction(int bankTransactionId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.IgnoreBankTransaction(bankTransactionId, cancellationToken);
            if (result.Success)
                await ReloadBank();

            return result;
        }, nameof(IgnoreBankTransaction));

    public Task<HttpResult> ReopenBankTransaction(int bankTransactionId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.ReopenBankTransaction(bankTransactionId, cancellationToken);
            if (result.Success)
                await ReloadBank();

            return result;
        }, nameof(ReopenBankTransaction));

    public async Task<HttpResult<BankRuleDto>> SaveBankRule(int? bankRuleId, SaveBankRuleModel model)
        => (HttpResult<BankRuleDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SaveBankRule(bankRuleId, model, cancellationToken);
            if (result.Success)
                await ReloadBank();

            return result;
        }, nameof(SaveBankRule));

    public Task<HttpResult> DeleteBankRule(int bankRuleId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeleteBankRule(bankRuleId, cancellationToken);
            if (result.Success)
                await ReloadBank();

            return result;
        }, nameof(DeleteBankRule));

    // ---------- invoices ----------

    public InvoiceSettingsDto? InvoiceSettings { get; private set; }
    public GetInvoicesResult? Invoices { get; private set; }
    public InvoiceStatus? InvoiceFilter { get; private set; }

    public Task LoadInvoices(InvoiceStatus? status) => RunInLoadingContextAsync(async cancellationToken =>
    {
        InvoiceFilter = status;
        var invoices = httpClient.GetInvoices(status, cancellationToken);
        var settings = httpClient.GetInvoiceSettings(cancellationToken);
        var invoicesResult = await invoices;
        if (InvoiceFilter == status)
            Invoices = invoicesResult is { Success: true, Dto: not null } i ? i.Dto : null;
        InvoiceSettings = await settings is { Success: true, Dto: not null } s ? s.Dto : null;
        OnAccountingChanged?.Invoke();
    }, nameof(LoadInvoices));

    public async Task<HttpResult<InvoiceSettingsDto>> SaveInvoiceSettings(SaveInvoiceSettingsModel model)
        => (HttpResult<InvoiceSettingsDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SaveInvoiceSettings(model, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                InvoiceSettings = result.Dto;
                OnAccountingChanged?.Invoke();
            }

            return result;
        }, nameof(SaveInvoiceSettings));

    public Task<HttpResult<InvoiceDetailDto>> GetInvoice(int invoiceId) => httpClient.GetInvoice(invoiceId);

    public async Task<HttpResult<InvoiceDetailDto>> SaveInvoice(int? invoiceId, SaveInvoiceModel model)
        => (HttpResult<InvoiceDetailDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.SaveInvoice(invoiceId, model, cancellationToken);
            if (result.Success)
                await LoadInvoices(InvoiceFilter);

            return result;
        }, nameof(SaveInvoice));

    public Task<HttpResult> DeleteInvoice(int invoiceId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeleteInvoice(invoiceId, cancellationToken);
            if (result.Success)
                await LoadInvoices(InvoiceFilter);

            return result;
        }, nameof(DeleteInvoice));

    public async Task<HttpResult<InvoiceDetailDto>> InvoiceAction(int invoiceId, string action, object? body)
        => (HttpResult<InvoiceDetailDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.InvoiceAction(invoiceId, action, body, cancellationToken);
            if (result.Success)
            {
                if (action == "pay")
                    await AfterReconciled();
                else
                    await LoadInvoices(InvoiceFilter);
            }

            return result;
        }, nameof(InvoiceAction));

    public Task<HttpResult<FileDownload>> GetInvoicePdf(int invoiceId) => httpClient.GetInvoicePdf(invoiceId);

    private Task ReloadBank() => BankTransactions is null && BankImports.Count == 0 ? Task.CompletedTask : LoadBank(BankAccountId, BankStatus);

    /// <summary>Entries were booked from suggestions or bank lines: everything that shows them.</summary>
    private async Task AfterReconciled()
    {
        // A bank line may have paid an invoice
        await Task.WhenAll(LoadProposals(), ReloadBank(), Invoices is null ? Task.CompletedTask : LoadInvoices(InvoiceFilter));
        await AfterEntryChanged();
    }

    private void SetBasics(AccountingDto basics)
    {
        Basics = basics;
        if (selectedYearId is null || basics.FiscalYears.All(y => y.Id != selectedYearId))
        {
            // The newest open year, else the newest one
            selectedYearId = (basics.FiscalYears.FirstOrDefault(y => y.Status == FiscalYearStatus.Open && y.StartDate <= DateOnly.FromDateTime(DateTime.Today))
                              ?? basics.FiscalYears.LastOrDefault(y => y.Status == FiscalYearStatus.Open)
                              ?? basics.FiscalYears.FirstOrDefault())?.Id;
        }

        OnAccountingChanged?.Invoke();
    }

    private async Task AfterEntryChanged()
    {
        await Task.WhenAll(LoadSummary(), LoadInbox(), Entries is null ? Task.CompletedTask : LoadEntries(Filter));
        // Categories and accounts become "used"
        await Load();
    }

    private async Task<HttpResult<FiscalYearDto>> SaveYear(Func<CancellationToken, Task<HttpResult<FiscalYearDto>>> call, string name)
        => (HttpResult<FiscalYearDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await call(cancellationToken);
            if (result.Success)
            {
                await Load();
                await LoadSummary();
            }

            return result;
        }, name);

    private async Task<HttpResult<T>> SaveAndReload<T>(Func<CancellationToken, Task<HttpResult<T>>> call, string name)
        => (HttpResult<T>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await call(cancellationToken);
            if (result.Success)
                await Load();

            return result;
        }, name);

    private Task<HttpResult> DeleteAndReload(Func<CancellationToken, Task<HttpResult>> call, string name)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await call(cancellationToken);
            if (result.Success)
                await Load();

            return result;
        }, name);
}
