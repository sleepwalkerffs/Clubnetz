using Bookennis.Api.Business.Accounting;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Accounting;

/// <summary>
/// The accounting of the club: fiscal years, money accounts, categories, cost centers, entries, receipts and reports.
/// Treasurers and admins keep the books, auditors (Rechnungsprüfer) read everything and record their audit.
/// Signed-in users only, never with an API key.
/// </summary>
public class AccountingController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    // ---------- basics ----------

    [HttpGet]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public async Task<AccountingDto> GetAccounting(int clubId, CancellationToken cancellationToken)
        => await mediator.Send(new GetAccounting(clubId, userAccessor.GetUserId(), await Passes(AuthorizationPolicies.AccountingManager), await Passes(AuthorizationPolicies.AccountingAuditor)), cancellationToken);

    /// <summary>Switches the accounting on with the first fiscal year, a bank account, a cash box and the categories of the Austrian template.</summary>
    [HttpPost("setup")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public async Task<AccountingDto> SetUpAccounting(int clubId, [FromBody] SetUpAccountingModel model, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetUpAccounting(clubId, model, DateTimeOffset.UtcNow), cancellationToken);
        return await GetAccounting(clubId, cancellationToken);
    }

    // ---------- fiscal years ----------

    /// <summary>Creates the fiscal year after the newest one.</summary>
    [HttpPost("fiscal-years")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<FiscalYearDto> CreateFiscalYear(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new CreateFiscalYear(clubId, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPut("fiscal-years/{fiscalYearId:int}/opening-balances")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<FiscalYearDto> SetOpeningBalances(int clubId, int fiscalYearId, [FromBody] SetOpeningBalancesModel model, CancellationToken cancellationToken)
        => mediator.Send(new SetOpeningBalances(clubId, fiscalYearId, model), cancellationToken);

    [HttpPost("fiscal-years/{fiscalYearId:int}/close")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<FiscalYearDto> CloseFiscalYear(int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => mediator.Send(new CloseFiscalYear(clubId, userAccessor.GetUserId(), fiscalYearId, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("fiscal-years/{fiscalYearId:int}/reopen")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<FiscalYearDto> ReopenFiscalYear(int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => mediator.Send(new ReopenFiscalYear(clubId, fiscalYearId), cancellationToken);

    /// <summary>The auditor records the audit of a closed fiscal year.</summary>
    [HttpPost("fiscal-years/{fiscalYearId:int}/audit")]
    [Authorize(AuthorizationPolicies.AccountingAuditor)]
    public Task<FiscalYearDto> RecordFiscalYearAudit(int clubId, int fiscalYearId, [FromBody] RecordAuditModel model, CancellationToken cancellationToken)
        => mediator.Send(new RecordFiscalYearAudit(clubId, userAccessor.GetUserId(), fiscalYearId, model, DateTimeOffset.UtcNow), cancellationToken);

    // ---------- reports ----------

    [HttpGet("fiscal-years/{fiscalYearId:int}/summary")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<AccountingSummaryDto> GetAccountingSummary(int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingSummary(clubId, fiscalYearId), cancellationToken);

    [HttpGet("fiscal-years/{fiscalYearId:int}/income-statement")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<IncomeStatementDto> GetIncomeStatement(int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => mediator.Send(new GetIncomeStatement(clubId, fiscalYearId), cancellationToken);

    [HttpGet("fiscal-years/{fiscalYearId:int}/cost-centers")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<CostCenterReportDto> GetCostCenterReport(int clubId, int fiscalYearId, CancellationToken cancellationToken)
        => mediator.Send(new GetCostCenterReport(clubId, fiscalYearId), cancellationToken);

    [HttpGet("fiscal-years/{fiscalYearId:int}/money-accounts/{moneyAccountId:int}/sheet")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<AccountSheetDto> GetAccountSheet(int clubId, int fiscalYearId, int moneyAccountId, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountSheet(clubId, fiscalYearId, moneyAccountId), cancellationToken);

    [HttpGet("fiscal-years/{fiscalYearId:int}/export")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public async Task<IActionResult> ExportAccounting(int clubId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportAccounting(clubId, fiscalYearId), cancellationToken);
        return File(result.Data, ExportAccountingResult.ContentType, result.FileName);
    }

    [HttpGet("fiscal-years/{fiscalYearId:int}/annual-report")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public async Task<IActionResult> GetAnnualReport(int clubId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAnnualReport(clubId, fiscalYearId, DateTimeOffset.UtcNow), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Data, AnnualReportFile.ContentType, result.FileName);
    }

    // ---------- money accounts, categories, cost centers ----------

    [HttpPost("money-accounts")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<MoneyAccountDto> CreateMoneyAccount(int clubId, [FromBody] SaveMoneyAccountModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateMoneyAccount(clubId, model), cancellationToken);

    [HttpPut("money-accounts/{moneyAccountId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<MoneyAccountDto> UpdateMoneyAccount(int clubId, int moneyAccountId, [FromBody] SaveMoneyAccountModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateMoneyAccount(clubId, moneyAccountId, model), cancellationToken);

    [HttpDelete("money-accounts/{moneyAccountId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DeleteMoneyAccount(int clubId, int moneyAccountId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteMoneyAccount(clubId, moneyAccountId), cancellationToken);

    [HttpPost("categories")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingCategoryDto> CreateAccountingCategory(int clubId, [FromBody] SaveAccountingCategoryModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateAccountingCategory(clubId, model), cancellationToken);

    [HttpPut("categories/{categoryId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingCategoryDto> UpdateAccountingCategory(int clubId, int categoryId, [FromBody] SaveAccountingCategoryModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateAccountingCategory(clubId, categoryId, model), cancellationToken);

    [HttpDelete("categories/{categoryId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DeleteAccountingCategory(int clubId, int categoryId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteAccountingCategory(clubId, categoryId), cancellationToken);

    [HttpPost("cost-centers")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<CostCenterDto> CreateCostCenter(int clubId, [FromBody] SaveCostCenterModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateCostCenter(clubId, model), cancellationToken);

    [HttpPut("cost-centers/{costCenterId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<CostCenterDto> UpdateCostCenter(int clubId, int costCenterId, [FromBody] SaveCostCenterModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateCostCenter(clubId, costCenterId, model), cancellationToken);

    [HttpDelete("cost-centers/{costCenterId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DeleteCostCenter(int clubId, int costCenterId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteCostCenter(clubId, costCenterId), cancellationToken);

    // ---------- entries ----------

    [HttpGet("fiscal-years/{fiscalYearId:int}/entries")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<GetAccountingEntriesResult> GetAccountingEntries(int clubId, int fiscalYearId, [FromQuery] int? moneyAccountId, [FromQuery] int? categoryId,
        [FromQuery] int? costCenterId, [FromQuery] string? search, [FromQuery] bool includeCancelled, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingEntries(clubId, fiscalYearId, moneyAccountId, categoryId, costCenterId, search, includeCancelled), cancellationToken);

    [HttpGet("entries/{entryId:int}")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<AccountingEntryDetailDto> GetAccountingEntry(int clubId, int entryId, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingEntry(clubId, entryId), cancellationToken);

    [HttpPost("entries")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingEntryDetailDto> CreateAccountingEntry(int clubId, [FromBody] SaveAccountingEntryModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateAccountingEntry(clubId, userAccessor.GetUserId(), model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPut("entries/{entryId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingEntryDetailDto> UpdateAccountingEntry(int clubId, int entryId, [FromBody] SaveAccountingEntryModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateAccountingEntry(clubId, userAccessor.GetUserId(), entryId, model, DateTimeOffset.UtcNow), cancellationToken);

    /// <summary>Entries are never deleted: a cancelled entry keeps its number and no longer counts.</summary>
    [HttpPost("entries/{entryId:int}/cancel")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingEntryDetailDto> CancelAccountingEntry(int clubId, int entryId, [FromBody] CancelAccountingEntryModel model, CancellationToken cancellationToken)
        => mediator.Send(new CancelAccountingEntry(clubId, userAccessor.GetUserId(), entryId, model.Reason, DateTimeOffset.UtcNow), cancellationToken);

    /// <summary>Members of the club to pick as who paid or was paid: names only.</summary>
    [HttpGet("members")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<List<AccountingMemberDto>> GetAccountingMembers(int clubId, [FromQuery] string? search, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingMembers(clubId, search), cancellationToken);

    // ---------- receipts ----------

    /// <summary>The receipts that wait for an entry.</summary>
    [HttpGet("receipts")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<List<AccountingReceiptDto>> GetAccountingReceiptInbox(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingReceiptInbox(clubId), cancellationToken);

    [HttpPost("receipts")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public Task<AccountingReceiptDto> UploadAccountingReceipt(int clubId, IFormFile file, CancellationToken cancellationToken)
        => mediator.Send(new UploadAccountingReceipt(clubId, userAccessor.GetUserId(), file, DateTimeOffset.UtcNow), cancellationToken);

    [HttpDelete("receipts/{receiptId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DeleteAccountingReceipt(int clubId, int receiptId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteAccountingReceipt(clubId, receiptId), cancellationToken);

    // Always served as a download (never inline): the files are uploaded by people
    [HttpGet("receipts/{receiptId:int}/file")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public async Task<IActionResult> GetAccountingReceiptFile(int clubId, int receiptId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAccountingReceiptFile(clubId, receiptId), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Data, result.ContentType, result.FileName);
    }

    private static readonly System.Text.Json.JsonSerializerOptions LayoutJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    // ---------- automatic entries ----------

    [HttpGet("automation")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<AccountingAutomationDto> GetAccountingAutomation(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingAutomation(clubId), cancellationToken);

    [HttpPut("automation")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingAutomationDto> UpdateAccountingAutomation(int clubId, [FromBody] SaveAccountingAutomationModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateAccountingAutomation(clubId, model, DateTimeOffset.UtcNow), cancellationToken);

    /// <summary>The suggested entries of a status (open by default).</summary>
    [HttpGet("proposals")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<List<AccountingProposalDto>> GetAccountingProposals(int clubId, [FromQuery] AccountingProposalStatus status, CancellationToken cancellationToken)
        => mediator.Send(new GetAccountingProposals(clubId, (Domain.Accounting.AccountingProposalStatus)status), cancellationToken);

    /// <summary>Collects what was paid since the last time (the job does it every hour).</summary>
    [HttpPost("proposals/sync")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<SyncAccountingProposalsResult> SyncAccountingProposals(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new SyncAccountingProposals(clubId, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("proposals/{proposalId:int}/book")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingEntryDetailDto> BookAccountingProposal(int clubId, int proposalId, [FromBody] BookAccountingProposalModel model, CancellationToken cancellationToken)
        => mediator.Send(new BookAccountingProposal(clubId, userAccessor.GetUserId(), proposalId, model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("proposals/{proposalId:int}/dismiss")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DismissAccountingProposal(int clubId, int proposalId, CancellationToken cancellationToken)
        => mediator.Send(new DismissAccountingProposal(clubId, proposalId), cancellationToken);

    // ---------- bank ----------

    /// <summary>Imports a bank statement (camt.053, MT940, CSV) into a money account. <paramref name="layout"/>: the mapped CSV columns as JSON.</summary>
    [HttpPost("bank-statements")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<BankImportResultDto> ImportBankStatement(int clubId, [FromForm] int moneyAccountId, IFormFile file, [FromForm] string? layout, CancellationToken cancellationToken)
    {
        if (file.Length > Domain.Accounting.BankStatementReader.MaxFileSize)
            throw new Domain.Exceptions.PreconditionException(Domain.Accounting.AccountingErrorCode.AccountingBankFileInvalid, "The file is too large.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        CsvColumnsDto? columns;
        try
        {
            columns = string.IsNullOrWhiteSpace(layout) ? null : System.Text.Json.JsonSerializer.Deserialize<CsvColumnsDto>(layout, LayoutJson);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new Domain.Exceptions.PreconditionException(Domain.Accounting.AccountingErrorCode.AccountingBankCsvLayoutInvalid, "The columns are no valid JSON.");
        }
        return await mediator.Send(new ImportBankStatement(clubId, userAccessor.GetUserId(), moneyAccountId, file.FileName, stream.ToArray(), columns, DateTimeOffset.UtcNow), cancellationToken);
    }

    [HttpGet("bank-statements")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<List<BankStatementImportDto>> GetBankStatementImports(int clubId, [FromQuery] int? moneyAccountId, CancellationToken cancellationToken)
        => mediator.Send(new GetBankStatementImports(clubId, moneyAccountId), cancellationToken);

    [HttpGet("bank-transactions")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<GetBankTransactionsResult> GetBankTransactions(int clubId, [FromQuery] int? moneyAccountId, [FromQuery] BankTransactionStatus status, CancellationToken cancellationToken)
        => mediator.Send(new GetBankTransactions(clubId, moneyAccountId, (Domain.Accounting.BankTransactionStatus)status), cancellationToken);

    [HttpPost("bank-transactions/{bankTransactionId:int}/match")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task MatchBankTransaction(int clubId, int bankTransactionId, [FromBody] MatchBankTransactionModel model, CancellationToken cancellationToken)
        => mediator.Send(new MatchBankTransaction(clubId, userAccessor.GetUserId(), bankTransactionId, model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("bank-transactions/{bankTransactionId:int}/book")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<AccountingEntryDetailDto> BookBankTransaction(int clubId, int bankTransactionId, [FromBody] SaveAccountingEntryModel model, CancellationToken cancellationToken)
        => mediator.Send(new BookBankTransaction(clubId, userAccessor.GetUserId(), bankTransactionId, model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("bank-transactions/book-by-rules")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<BookBankTransactionsByRulesResult> BookBankTransactionsByRules(int clubId, [FromQuery] int? moneyAccountId, CancellationToken cancellationToken)
        => mediator.Send(new BookBankTransactionsByRules(clubId, userAccessor.GetUserId(), moneyAccountId, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("bank-transactions/{bankTransactionId:int}/ignore")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task IgnoreBankTransaction(int clubId, int bankTransactionId, CancellationToken cancellationToken)
        => mediator.Send(new IgnoreBankTransaction(clubId, bankTransactionId), cancellationToken);

    [HttpPost("bank-transactions/{bankTransactionId:int}/reopen")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task ReopenBankTransaction(int clubId, int bankTransactionId, CancellationToken cancellationToken)
        => mediator.Send(new ReopenBankTransaction(clubId, bankTransactionId), cancellationToken);

    [HttpGet("bank-rules")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<List<BankRuleDto>> GetBankRules(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetBankRules(clubId), cancellationToken);

    [HttpPost("bank-rules")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<BankRuleDto> CreateBankRule(int clubId, [FromBody] SaveBankRuleModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateBankRule(clubId, model), cancellationToken);

    [HttpPut("bank-rules/{bankRuleId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<BankRuleDto> UpdateBankRule(int clubId, int bankRuleId, [FromBody] SaveBankRuleModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateBankRule(clubId, bankRuleId, model), cancellationToken);

    [HttpDelete("bank-rules/{bankRuleId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DeleteBankRule(int clubId, int bankRuleId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteBankRule(clubId, bankRuleId), cancellationToken);

    // ---------- invoices ----------

    [HttpGet("invoice-settings")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<InvoiceSettingsDto> GetInvoiceSettings(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetInvoiceSettings(clubId), cancellationToken);

    [HttpPut("invoice-settings")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceSettingsDto> SaveInvoiceSettings(int clubId, [FromBody] SaveInvoiceSettingsModel model, CancellationToken cancellationToken)
        => mediator.Send(new SaveInvoiceSettings(clubId, model), cancellationToken);

    [HttpGet("invoices")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<GetInvoicesResult> GetInvoices(int clubId, [FromQuery] InvoiceStatus? status, CancellationToken cancellationToken)
        => mediator.Send(new GetInvoices(clubId, (Domain.Accounting.InvoiceStatus?)status, DateTimeOffset.UtcNow), cancellationToken);

    [HttpGet("invoices/{invoiceId:int}")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public Task<InvoiceDetailDto> GetInvoice(int clubId, int invoiceId, CancellationToken cancellationToken)
        => mediator.Send(new GetInvoice(clubId, invoiceId, DateTimeOffset.UtcNow), cancellationToken);

    [HttpGet("invoices/{invoiceId:int}/pdf")]
    [Authorize(AuthorizationPolicies.AccountingReader)]
    public async Task<IActionResult> GetInvoicePdf(int clubId, int invoiceId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInvoicePdf(clubId, invoiceId), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Data, InvoicePdfFile.ContentType, result.FileName);
    }

    [HttpPost("invoices")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> CreateInvoice(int clubId, [FromBody] SaveInvoiceModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateInvoice(clubId, model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPut("invoices/{invoiceId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> UpdateInvoice(int clubId, int invoiceId, [FromBody] SaveInvoiceModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateInvoice(clubId, invoiceId, model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpDelete("invoices/{invoiceId:int}")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task DeleteInvoice(int clubId, int invoiceId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteInvoice(clubId, invoiceId), cancellationToken);

    [HttpPost("invoices/{invoiceId:int}/issue")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> IssueInvoice(int clubId, int invoiceId, CancellationToken cancellationToken)
        => mediator.Send(new IssueInvoice(clubId, invoiceId, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("invoices/{invoiceId:int}/send")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> SendInvoice(int clubId, int invoiceId, [FromBody] SendInvoiceModel model, CancellationToken cancellationToken)
        => mediator.Send(new SendInvoice(clubId, invoiceId, model.Email, false, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("invoices/{invoiceId:int}/remind")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> RemindInvoice(int clubId, int invoiceId, [FromBody] SendInvoiceModel model, CancellationToken cancellationToken)
        => mediator.Send(new SendInvoice(clubId, invoiceId, model.Email, true, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("invoices/{invoiceId:int}/pay")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> PayInvoice(int clubId, int invoiceId, [FromBody] PayInvoiceModel model, CancellationToken cancellationToken)
        => mediator.Send(new PayInvoice(clubId, userAccessor.GetUserId(), invoiceId, model, DateTimeOffset.UtcNow), cancellationToken);

    [HttpPost("invoices/{invoiceId:int}/cancel")]
    [Authorize(AuthorizationPolicies.AccountingManager)]
    public Task<InvoiceDetailDto> CancelInvoice(int clubId, int invoiceId, [FromBody] CancelInvoiceModel model, CancellationToken cancellationToken)
        => mediator.Send(new CancelInvoice(clubId, invoiceId, model.Reason, DateTimeOffset.UtcNow), cancellationToken);

    private async Task<bool> Passes(string policy) => (await authorizationService.AuthorizeAsync(User, policy)).Succeeded;
}
