using System.Globalization;
using System.Resources;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Accounting;
using Bookennis.Shared.Controller.Accounting;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using AccountingCategoryKind = Bookennis.Domain.Accounting.AccountingCategoryKind;
using AccountingEntryKind = Bookennis.Domain.Accounting.AccountingEntryKind;
using AccountingEntryStatus = Bookennis.Domain.Accounting.AccountingEntryStatus;

namespace Bookennis.Api.Business.Accounting;

/// <summary>A fiscal year as an Excel workbook for the tax adviser and the auditors, in the language of the request.</summary>
public record ExportAccounting(int ClubId, int FiscalYearId) : IQuery<ExportAccountingResult>
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor, IMediator mediator) : IRequestHandler<ExportAccounting, ExportAccountingResult>
    {
        public async Task<ExportAccountingResult> Handle(ExportAccounting request, CancellationToken cancellationToken)
        {
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            var entries = await context.AccountingEntries.AsNoTracking().Include(e => e.Lines)
                .Where(e => e.ClubId == request.ClubId && e.FiscalYearId == year.Id)
                .OrderBy(e => e.Date).ThenBy(e => e.Number)
                .ToListAsync(cancellationToken);
            var receipts = await context.AccountingReceipts
                .Where(r => r.ClubId == request.ClubId && r.EntryId != null)
                .GroupBy(r => r.EntryId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

            var data = new AccountingExportData(
                year,
                entries,
                receipts,
                await context.MoneyAccounts.AsNoTracking().Where(a => a.ClubId == request.ClubId).ToDictionaryAsync(a => a.Id, cancellationToken),
                await context.AccountingCategories.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToDictionaryAsync(c => c.Id, cancellationToken),
                await context.CostCenters.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken),
                await mediator.Send(new GetAccountingSummary(request.ClubId, year.Id), cancellationToken),
                await mediator.Send(new GetIncomeStatement(request.ClubId, year.Id), cancellationToken));

            var exporter = new AccountingExcelExporter(languageAccessor.GetCultureInfo());
            var fileYear = year.Label.Replace('/', '-');
            return new ExportAccountingResult(exporter.Export(data), $"{exporter.FileName} {fileYear}.xlsx");
        }
    }
}

public record ExportAccountingResult(byte[] Data, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public record AccountingExportData(
    FiscalYear Year,
    List<AccountingEntry> Entries,
    Dictionary<int, int> ReceiptCounts,
    Dictionary<int, MoneyAccount> Accounts,
    Dictionary<int, AccountingCategory> Categories,
    Dictionary<int, string> CostCenters,
    AccountingSummaryDto Summary,
    IncomeStatementDto IncomeStatement);

/// <summary>
/// Three sheets: the journal (one row per line, transfers in one row with their own amount column, cancelled entries struck through), the income/expense statement by tax
/// sphere and the overview of the money accounts. Amounts are numbers, so the sheets can be summed up.
/// </summary>
public class AccountingExcelExporter(CultureInfo culture)
{
    private static readonly ResourceManager Texts = new("Bookennis.Api.Resources.Localization.Export.Accounting", typeof(AccountingExcelExporter).Assembly);
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#E2E8F0");
    private static readonly XLColor SumColor = XLColor.FromHtml("#F1F5F9");
    private const string AmountFormat = "#,##0.00";

    public string FileName => T("FileName");

    public byte[] Export(AccountingExportData data)
    {
        using var workbook = new XLWorkbook();

        AddJournal(workbook, data);
        AddIncomeStatement(workbook, data);
        AddMoneyAccounts(workbook, data);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddJournal(XLWorkbook workbook, AccountingExportData data)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetJournal"));
        var headers = new List<string> { T("Number"), T("Date"), T("Text"), T("Counterparty"), T("MoneyAccount"), T("Category"), T("CategoryNumber"), T("CostCenter"), T("Income"), T("Expense"), T("TransferAmount"), T("Receipts"), T("Status") };
        WriteHeader(sheet, headers);

        var row = 1;
        foreach (var entry in data.Entries)
        {
            var status = entry.Status == AccountingEntryStatus.Cancelled ? T("Cancelled") : "";
            var receipts = data.ReceiptCounts.GetValueOrDefault(entry.Id);

            if (entry.Kind == AccountingEntryKind.Transfer)
            {
                row++;
                WriteEntry(sheet, row, entry, data, receipts, status);
                var to = entry.ToMoneyAccountId is { } toId ? data.Accounts[toId].Name : "";
                sheet.Cell(row, 5).Value = $"{data.Accounts[entry.MoneyAccountId].Name} → {to}";
                sheet.Cell(row, 6).Value = T("Transfer");
                if (status == "")
                    WriteAmount(sheet.Cell(row, 11), entry.TransferAmountCents ?? 0);
                continue;
            }

            foreach (var line in entry.Lines.OrderBy(l => l.Position))
            {
                row++;
                WriteEntry(sheet, row, entry, data, receipts, status);
                var category = data.Categories[line.CategoryId];
                sheet.Cell(row, 6).Value = category.Name;
                sheet.Cell(row, 7).Value = category.Number ?? "";
                sheet.Cell(row, 8).Value = line.CostCenterId is { } costCenterId ? data.CostCenters.GetValueOrDefault(costCenterId, "") : "";
                if (status == "")
                    WriteAmount(sheet.Cell(row, category.Kind == AccountingCategoryKind.Income ? 9 : 10), line.AmountCents);
            }
        }

        FinishSheet(sheet, headers.Count, row);
    }

    private void WriteEntry(IXLWorksheet sheet, int row, AccountingEntry entry, AccountingExportData data, int receipts, string status)
    {
        sheet.Cell(row, 1).Value = entry.Number;
        sheet.Cell(row, 2).Value = entry.Date.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(row, 2).Style.DateFormat.Format = culture.DateTimeFormat.ShortDatePattern;
        sheet.Cell(row, 3).Value = entry.Text;
        sheet.Cell(row, 4).Value = entry.CounterpartyName ?? "";
        sheet.Cell(row, 5).Value = data.Accounts[entry.MoneyAccountId].Name;
        sheet.Cell(row, 12).Value = receipts;
        sheet.Cell(row, 13).Value = status;
        if (status != "")
            sheet.Row(row).Style.Font.Strikethrough = true;
    }

    private void AddIncomeStatement(XLWorkbook workbook, AccountingExportData data)
    {
        var statement = data.IncomeStatement;
        var sheet = workbook.Worksheets.Add(SheetName("SheetIncomeStatement"));
        var hasPrevious = statement.PreviousFiscalYearId is not null;
        var headers = new List<string> { T("CategoryNumber"), T("Category"), data.Year.Label };
        if (hasPrevious)
            headers.Add(T("PreviousYear"));
        WriteHeader(sheet, headers);

        var row = 1;
        foreach (var sphere in statement.Spheres)
        {
            row += 2;
            sheet.Cell(row, 1).Value = T($"Sphere_{sphere.TaxSphere}");
            sheet.Cell(row, 1).Style.Font.Bold = true;

            foreach (var kind in new[] { global::Bookennis.Shared.Controller.Accounting.AccountingCategoryKind.Income, global::Bookennis.Shared.Controller.Accounting.AccountingCategoryKind.Expense })
            {
                foreach (var line in sphere.Categories.Where(c => c.Kind == kind))
                {
                    row++;
                    var category = data.Categories[line.CategoryId];
                    sheet.Cell(row, 1).Value = category.Number ?? "";
                    sheet.Cell(row, 2).Value = $"{category.Name} ({T(kind.ToString())})";
                    WriteAmount(sheet.Cell(row, 3), line.AmountCents);
                    if (hasPrevious)
                        WriteAmount(sheet.Cell(row, 4), line.PreviousAmountCents ?? 0);
                }
            }

            row++;
            sheet.Cell(row, 2).Value = T("SphereResult");
            WriteAmount(sheet.Cell(row, 3), sphere.IncomeCents - sphere.ExpenseCents);
            if (hasPrevious)
                WriteAmount(sheet.Cell(row, 4), (sphere.PreviousIncomeCents ?? 0) - (sphere.PreviousExpenseCents ?? 0));
            sheet.Range(row, 1, row, headers.Count).Style.Fill.BackgroundColor = SumColor;
        }

        row += 2;
        foreach (var (key, current, previous) in new[]
                 {
                     ("TotalIncome", statement.IncomeCents, statement.PreviousIncomeCents),
                     ("TotalExpense", statement.ExpenseCents, statement.PreviousExpenseCents),
                     ("Result", statement.IncomeCents - statement.ExpenseCents, statement.PreviousIncomeCents - statement.PreviousExpenseCents),
                 })
        {
            sheet.Cell(row, 2).Value = T(key);
            sheet.Cell(row, 2).Style.Font.Bold = true;
            WriteAmount(sheet.Cell(row, 3), current);
            if (hasPrevious)
                WriteAmount(sheet.Cell(row, 4), previous ?? 0);
            row++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private void AddMoneyAccounts(XLWorkbook workbook, AccountingExportData data)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetMoneyAccounts"));
        var headers = new List<string> { T("MoneyAccount"), T("Iban"), T("Opening"), T("Income"), T("Expense"), T("TransfersIn"), T("TransfersOut"), T("Closing") };
        WriteHeader(sheet, headers);

        var row = 1;
        foreach (var balance in data.Summary.Accounts)
        {
            row++;
            var account = data.Accounts[balance.MoneyAccountId];
            sheet.Cell(row, 1).Value = account.Name;
            sheet.Cell(row, 2).Value = account.Iban is { } iban ? IbanNumber.Format(iban) : "";
            WriteAmount(sheet.Cell(row, 3), balance.OpeningCents);
            WriteAmount(sheet.Cell(row, 4), balance.IncomeCents);
            WriteAmount(sheet.Cell(row, 5), balance.ExpenseCents);
            WriteAmount(sheet.Cell(row, 6), balance.TransfersInCents);
            WriteAmount(sheet.Cell(row, 7), balance.TransfersOutCents);
            WriteAmount(sheet.Cell(row, 8), balance.ClosingCents);
        }

        row++;
        sheet.Cell(row, 1).Value = T("Total");
        sheet.Row(row).Style.Font.Bold = true;
        WriteAmount(sheet.Cell(row, 3), data.Summary.OpeningCents);
        WriteAmount(sheet.Cell(row, 4), data.Summary.IncomeCents);
        WriteAmount(sheet.Cell(row, 5), data.Summary.ExpenseCents);
        WriteAmount(sheet.Cell(row, 8), data.Summary.ClosingCents);
        sheet.Range(row, 1, row, headers.Count).Style.Fill.BackgroundColor = SumColor;

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private static void WriteAmount(IXLCell cell, long cents)
    {
        cell.Value = cents / 100m;
        cell.Style.NumberFormat.Format = AmountFormat;
    }

    private static void WriteHeader(IXLWorksheet sheet, List<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderColor;
        }
    }

    private static void FinishSheet(IXLWorksheet sheet, int columnCount, int lastRow)
    {
        sheet.SheetView.FreezeRows(1);
        if (lastRow > 1)
            sheet.Range(1, 1, lastRow, columnCount).SetAutoFilter();

        sheet.Columns().AdjustToContents();
    }

    // Sheet names are limited to 31 characters
    private string SheetName(string key)
    {
        var name = T(key);
        return name.Length > 31 ? name[..31] : name;
    }

    private string T(string key) => Texts.GetString(key, culture) ?? key;
}
