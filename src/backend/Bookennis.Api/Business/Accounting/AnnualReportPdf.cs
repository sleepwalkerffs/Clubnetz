using System.Globalization;
using System.Resources;
using Bookennis.Domain.Accounting;
using Bookennis.Shared.Controller.Accounting;
using SkiaSharp;
using static Bookennis.Api.Business.Accounting.PdfKit;
using AccountingCategoryKind = Bookennis.Shared.Controller.Accounting.AccountingCategoryKind;
using FiscalYearAuditStatus = Bookennis.Domain.Accounting.FiscalYearAuditStatus;
using FiscalYearStatus = Bookennis.Domain.Accounting.FiscalYearStatus;

namespace Bookennis.Api.Business.Accounting;

/// <summary>What the annual report shows: the year, its statement and its money accounts, with the names to print.</summary>
public record AnnualReportPdfData(
    string ClubName,
    FiscalYear Year,
    string? PreviousYearLabel,
    string? AuditedByName,
    IncomeStatementDto Statement,
    AccountingSummaryDto Summary,
    IReadOnlyDictionary<int, AccountingCategory> Categories,
    IReadOnlyDictionary<int, MoneyAccount> Accounts,
    DateOnly Created);

/// <summary>
/// Draws the annual report of a fiscal year for the general meeting as an A4 PDF, in the style of the invoice: the income/expense statement
/// (Einnahmen-Ausgaben-Rechnung) by tax sphere next to the year before, the money accounts (Vermögensübersicht) and the audit.
/// The texts come from the resources of the Excel export, in the language of the request.
/// </summary>
public class AnnualReportPdf(CultureInfo culture)
{
    private static readonly ResourceManager Resources = new("Bookennis.Api.Resources.Localization.Export.Accounting", typeof(AnnualReportPdf).Assembly);

    private const float ContentBottom = PageHeight - 70;
    private const float RowHeight = 16;

    // Austrian formats in both languages: the amounts are euros (en-GB would print pounds)
    private readonly CultureInfo format = CultureInfo.GetCultureInfo(culture.TwoLetterISOLanguageName == "de" ? "de-AT" : "en-AT");
    private bool German => culture.TwoLetterISOLanguageName == "de";

    public string FileName(FiscalYear year) => $"{T("AnnualReport")} {year.Label.Replace('/', '-')}.pdf";

    public byte[] Render(AnnualReportPdfData data)
    {
        using var stream = new MemoryStream();
        // Two passes: the first counts the pages for "page 1 of 2"
        var pageCount = new Drawing(this, data, null, 0).Run();
        using (var document = SKDocument.CreatePdf(stream, Metadata($"{T("AnnualReport")} {data.Year.Label}", data.ClubName)))
            new Drawing(this, data, document, pageCount).Run();
        return stream.ToArray();
    }

    private string T(string key) => Resources.GetString(key, culture) ?? key;

    private string Money(long cents) => cents < 0 ? "−" + (-cents / 100m).ToString("C", format) : (cents / 100m).ToString("C", format);

    private string Signed(long cents) => cents switch
    {
        > 0 => "+ " + Money(cents),
        < 0 => "− " + Money(-cents),
        _ => Money(0),
    };

    private string Day(DateOnly date) => date.ToString(German ? "dd.MM.yyyy" : "d MMM yyyy", format);

    /// <summary>One pass over the pages: without a document it only counts them.</summary>
    private sealed class Drawing(AnnualReportPdf pdf, AnnualReportPdfData data, SKDocument? document, int pageCount)
    {
        private readonly SKPictureRecorder? recorder = document is null ? new SKPictureRecorder() : null;
        private SKCanvas canvas = null!;
        private int page;
        private float y;

        /// <summary>Repeated at the top of a new page while a table runs on.</summary>
        private Action? tableHeader;

        public int Run()
        {
            BeginPage();
            Head();
            IncomeStatement();
            MoneyAccounts();
            Audit();
            EndPage();
            document?.Close();
            recorder?.Dispose();
            return page;
        }

        private string T(string key) => pdf.T(key);

        // ---------- pages ----------

        private void BeginPage()
        {
            page++;
            canvas = document?.BeginPage(PageWidth, PageHeight) ?? recorder!.BeginRecording(new SKRect(0, 0, PageWidth, PageHeight));
            y = Margin + 10;
        }

        private void EndPage()
        {
            var footerY = PageHeight - 40;
            Rule(canvas, footerY - 14);
            Text(canvas, $"{data.ClubName} · {T("AnnualReport")} {data.Year.Label}", Margin, footerY, 8, color: Muted);
            if (pageCount > 1)
                Text(canvas, string.Format(CultureInfo.InvariantCulture, T("Page"), page, pageCount), Right, footerY, 8, color: Muted, align: SKTextAlign.Right);

            if (document is not null)
                document.EndPage();
            else
                recorder!.EndRecording().Dispose();
        }

        /// <summary>Starts a new page when <paramref name="height"/> does not fit any more; a running table gets its header again.</summary>
        private void Need(float height)
        {
            if (y + height <= ContentBottom)
                return;

            EndPage();
            BeginPage();
            Text(canvas, $"{T("AnnualReport")} {data.Year.Label}", Margin, y, 10, bold: true, Muted);
            y += 24;
            tableHeader?.Invoke();
        }

        // ---------- head: the club on the left, the year on the right ----------

        private void Head()
        {
            var year = data.Year;
            Text(canvas, data.ClubName, Margin, y, 16, bold: true, Accent);
            var leftY = y + 18;
            foreach (var line in Wrap(T("AnnualReportSubtitle"), (Right - Margin) / 2 - 10, 9, bold: false))
            {
                Text(canvas, line, Margin, leftY, 9, color: Muted);
                leftY += 12;
            }

            Text(canvas, T("AnnualReport").ToUpper(pdf.format), Right, y, 20, bold: true, Ink, SKTextAlign.Right);
            var detailY = y + 22;
            void Detail(string label, string value)
            {
                Text(canvas, label, Right - 130, detailY, 9, color: Muted, align: SKTextAlign.Right);
                foreach (var line in Wrap(value, 120, 9, bold: true))
                {
                    Text(canvas, line, Right, detailY, 9, bold: true, Ink, SKTextAlign.Right);
                    detailY += 13;
                }
            }

            Detail(T("FiscalYear"), year.Label);
            Detail(T("Period"), $"{pdf.Day(year.StartDate)} – {pdf.Day(year.EndDate)}");
            Detail(T("StatusLabel"), T(year.Status == FiscalYearStatus.Open ? "StatusOpen" : "StatusClosed"));
            Detail(T("AuditLabel"), T(year.AuditStatus switch
            {
                FiscalYearAuditStatus.Audited => "Audited",
                FiscalYearAuditStatus.AuditedWithRemarks => "AuditedWithRemarks",
                _ => "NotAudited",
            }));
            Detail(T("Created"), pdf.Day(data.Created));

            y = Math.Max(leftY, detailY) + 20;
        }

        private void SectionTitle(string title, string hint)
        {
            Need(70);
            Text(canvas, title, Margin, y, 13, bold: true, Ink);
            y += 15;
            foreach (var line in Wrap(hint, Right - Margin, 9, bold: false))
            {
                Text(canvas, line, Margin, y, 9, color: Muted);
                y += 12;
            }
            y += 6;
        }

        // ---------- income and expenses ----------

        private void IncomeStatement()
        {
            var statement = data.Statement;
            var hasPrevious = data.PreviousYearLabel is not null;
            const float colNumber = Margin;
            const float colName = Margin + 38;
            var colCurrent = hasPrevious ? Right - 100 : Right;
            const float colPrevious = Right;

            SectionTitle(T("SheetIncomeStatement"), T("IncomeStatementHint"));
            if (statement.Spheres.Count == 0)
            {
                Text(canvas, T("NoEntries"), Margin, y + 4, 10, color: Muted);
                y += 34;
                return;
            }

            void Header()
            {
                Text(canvas, T("Number"), colNumber, y, 8.5f, bold: true, Muted);
                Text(canvas, T("Category"), colName, y, 8.5f, bold: true, Muted);
                Text(canvas, data.Year.Label, colCurrent, y, 8.5f, bold: true, Muted, SKTextAlign.Right);
                if (hasPrevious)
                    Text(canvas, data.PreviousYearLabel!, colPrevious, y, 8.5f, bold: true, Muted, SKTextAlign.Right);
                y += 6;
                Rule(canvas, y);
                y += 14;
            }

            void Amounts(long current, long? previous, float size, bool bold)
            {
                Text(canvas, pdf.Signed(current), colCurrent, y, size, bold, Ink, SKTextAlign.Right);
                if (hasPrevious)
                    Text(canvas, pdf.Signed(previous ?? 0), colPrevious, y, size, bold, Muted, SKTextAlign.Right);
            }

            tableHeader = Header;
            Header();
            foreach (var sphere in statement.Spheres)
            {
                // The name of the area stays with its first line
                Need(RowHeight * 2 + 6);
                y += 4;
                Text(canvas, T($"Sphere_{sphere.TaxSphere}"), colNumber, y, 10, bold: true, Ink);
                y += RowHeight;

                foreach (var kind in new[] { AccountingCategoryKind.Income, AccountingCategoryKind.Expense })
                {
                    foreach (var line in sphere.Categories.Where(c => c.Kind == kind))
                    {
                        var category = data.Categories[line.CategoryId];
                        var name = Wrap(category.Name, colCurrent - colName - 110, 10, bold: false);
                        Need(name.Count * 13 + 2);
                        var sign = kind == AccountingCategoryKind.Income ? 1 : -1;
                        Text(canvas, category.Number ?? "", colNumber, y, 10, color: Muted);
                        Amounts(sign * line.AmountCents, sign * line.PreviousAmountCents, 10, bold: false);
                        foreach (var part in name)
                        {
                            Text(canvas, part, colName, y, 10, color: Ink);
                            y += 13;
                        }
                        y += 2;
                    }
                }

                Need(RowHeight + 4);
                Rule(canvas, y - 10, colName);
                y += 3;
                Text(canvas, T("SphereResult"), colName, y, 10, bold: true, Ink);
                Amounts(sphere.IncomeCents - sphere.ExpenseCents, sphere.PreviousIncomeCents - sphere.PreviousExpenseCents, 10, bold: true);
                y += RowHeight + 4;
            }

            Need(RowHeight * 2 + 46);
            Rule(canvas, y - 8, color: Ink);
            y += 8;
            Text(canvas, T("TotalIncome"), colName, y, 10, color: Ink);
            Text(canvas, pdf.Money(statement.IncomeCents), colCurrent, y, 10, color: Ink, align: SKTextAlign.Right);
            if (hasPrevious)
                Text(canvas, pdf.Money(statement.PreviousIncomeCents ?? 0), colPrevious, y, 10, color: Muted, align: SKTextAlign.Right);
            y += RowHeight;
            Text(canvas, T("TotalExpense"), colName, y, 10, color: Ink);
            Text(canvas, pdf.Money(statement.ExpenseCents), colCurrent, y, 10, color: Ink, align: SKTextAlign.Right);
            if (hasPrevious)
                Text(canvas, pdf.Money(statement.PreviousExpenseCents ?? 0), colPrevious, y, 10, color: Muted, align: SKTextAlign.Right);
            y += 14;

            // The result in a panel, like the payment box of the invoice
            Box(canvas, new SKRect(Margin, y, Right, y + 30), Panel);
            y += 20;
            Text(canvas, T("Result"), colName, y, 12, bold: true, Ink);
            Amounts(statement.IncomeCents - statement.ExpenseCents, statement.PreviousIncomeCents - statement.PreviousExpenseCents, 12, bold: true);
            y += 38;
            tableHeader = null;
        }

        // ---------- money accounts ----------

        private void MoneyAccounts()
        {
            var summary = data.Summary;
            const float step = 74;
            float[] columns = [Right - 4 * step, Right - 3 * step, Right - 2 * step, Right - step, Right];
            var nameWidth = columns[0] - Margin - 70;

            SectionTitle(T("MoneyAccountsTitle"), T("MoneyAccountsHint"));

            void Header()
            {
                Text(canvas, T("MoneyAccount"), Margin, y, 8.5f, bold: true, Muted);
                string[] labels = [T("Opening"), T("Incomes"), T("Expenses"), T("Transfers"), T("Closing")];
                for (var i = 0; i < labels.Length; i++)
                    Text(canvas, labels[i], columns[i], y, 8.5f, bold: true, Muted, SKTextAlign.Right);
                y += 6;
                Rule(canvas, y);
                y += 14;
            }

            tableHeader = Header;
            Header();
            var last = summary.Accounts.LastOrDefault();
            foreach (var balance in summary.Accounts)
            {
                var account = data.Accounts[balance.MoneyAccountId];
                var name = Wrap(account.Name, nameWidth, 10, bold: false);
                var iban = account.Iban is { } value ? IbanNumber.Format(value) : null;
                // The last account keeps the total with it
                Need(name.Count * 13 + (iban is null ? 0 : 11) + 4 + (balance == last ? 30 : 0));

                var transfers = balance.TransfersInCents - balance.TransfersOutCents;
                Text(canvas, pdf.Money(balance.OpeningCents), columns[0], y, 9.5f, color: Ink, align: SKTextAlign.Right);
                Text(canvas, pdf.Money(balance.IncomeCents), columns[1], y, 9.5f, color: Ink, align: SKTextAlign.Right);
                Text(canvas, pdf.Money(balance.ExpenseCents), columns[2], y, 9.5f, color: Ink, align: SKTextAlign.Right);
                Text(canvas, transfers == 0 ? "–" : pdf.Signed(transfers), columns[3], y, 9.5f, color: Ink, align: SKTextAlign.Right);
                Text(canvas, pdf.Money(balance.ClosingCents), columns[4], y, 9.5f, bold: true, Ink, SKTextAlign.Right);
                foreach (var part in name)
                {
                    Text(canvas, part, Margin, y, 10, color: Ink);
                    y += 13;
                }
                if (iban is not null)
                {
                    Text(canvas, iban, Margin, y - 1, 8, color: Muted);
                    y += 11;
                }
                y += 4;
            }

            Need(30);
            Rule(canvas, y - 10, color: Ink);
            y += 6;
            Text(canvas, T("Total"), Margin, y, 10.5f, bold: true, Ink);
            Text(canvas, pdf.Money(summary.OpeningCents), columns[0], y, 10, bold: true, Ink, SKTextAlign.Right);
            Text(canvas, pdf.Money(summary.IncomeCents), columns[1], y, 10, bold: true, Ink, SKTextAlign.Right);
            Text(canvas, pdf.Money(summary.ExpenseCents), columns[2], y, 10, bold: true, Ink, SKTextAlign.Right);
            Text(canvas, pdf.Money(summary.ClosingCents), columns[4], y, 10, bold: true, Ink, SKTextAlign.Right);
            y += 40;
            tableHeader = null;
        }

        // ---------- audit ----------

        private void Audit()
        {
            var year = data.Year;
            if (year.AuditStatus == FiscalYearAuditStatus.NotAudited || year.AuditedAt is not { } auditedAt)
                return;

            var lines = new List<(string Text, bool Muted)>();
            var date = pdf.Day(AccountingQueries.Today(auditedAt));
            foreach (var part in Wrap(string.Format(pdf.format, T("AuditedBy"), data.AuditedByName ?? "—", date), Right - Margin - 28, 10, bold: false))
                lines.Add((part, false));
            if (year.AuditComment is { } comment)
            {
                foreach (var part in comment.Split('\n').SelectMany(p => Wrap(p, Right - Margin - 28, 9.5f, bold: false)))
                    lines.Add((part, true));
            }

            var height = 40 + lines.Count * 13;
            Need(height);
            Box(canvas, new SKRect(Margin, y, Right, y + height - 10), Panel);
            y += 20;
            Text(canvas, T("AuditLabel"), Margin + 14, y, 11, bold: true, Ink);
            y += 16;
            foreach (var (text, muted) in lines)
            {
                Text(canvas, text, Margin + 14, y, muted ? 9.5f : 10, color: muted ? Muted : Ink);
                y += 13;
            }
            y += 14;
        }
    }
}
