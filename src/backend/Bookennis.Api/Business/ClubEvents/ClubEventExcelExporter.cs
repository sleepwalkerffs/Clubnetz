using System.Globalization;
using System.Resources;
using Bookennis.Shared.Controller.ClubEvents;
using ClosedXML.Excel;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Renders the registrations of a club event as an Excel workbook (one row per registration, a column per option, plus a summary sheet).</summary>
public class ClubEventExcelExporter(CultureInfo culture)
{
    private static readonly ResourceManager Texts = new("Bookennis.Api.Resources.Localization.Export.ClubEvent", typeof(ClubEventExcelExporter).Assembly);

    private static readonly XLColor HeaderColor = XLColor.FromHtml("#E2E8F0");
    private static readonly XLColor QuestionHeaderColor = XLColor.FromHtml("#CBD5E1");

    public byte[] Export(ClubEventDto clubEvent)
    {
        using var workbook = new XLWorkbook();

        AddParticipantsSheet(workbook, clubEvent);
        AddSummarySheet(workbook, clubEvent);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddParticipantsSheet(XLWorkbook workbook, ClubEventDto clubEvent)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetParticipants"));
        var options = clubEvent.Questions.SelectMany(q => q.Options.Select(o => (Question: q, Option: o))).ToList();

        // Row 1: question texts spanning their options, row 2: column headers
        var fixedHeaders = new List<string> { T("Name"), T("HeadCount") };
        var column = fixedHeaders.Count + 1;
        foreach (var question in clubEvent.Questions)
        {
            var range = sheet.Range(1, column, 1, column + question.Options.Count - 1);
            range.Merge();
            range.Value = question.Text;
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = QuestionHeaderColor;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            column += question.Options.Count;
        }

        var headers = fixedHeaders.Concat(options.Select(o => o.Option.Label)).Append(T("Comment")).Append(T("RegisteredAt")).ToList();
        WriteHeader(sheet, 2, headers);

        var row = 3;
        foreach (var registration in clubEvent.Registrations)
        {
            var answers = registration.Answers.ToDictionary(a => a.OptionId, a => a.Quantity);
            sheet.Cell(row, 1).Value = $"{registration.FirstName} {registration.LastName}";
            sheet.Cell(row, 2).Value = registration.HeadCount;

            for (var i = 0; i < options.Count; i++)
            {
                if (answers.TryGetValue(options[i].Option.Id, out var quantity))
                    sheet.Cell(row, 3 + i).Value = quantity;
            }

            sheet.Cell(row, 3 + options.Count).Value = registration.Comment ?? "";
            // Date only: the club's time zone is unknown here
            sheet.Cell(row, 4 + options.Count).Value = registration.RegisteredAt.UtcDateTime.Date;
            sheet.Cell(row, 4 + options.Count).Style.DateFormat.Format = culture.DateTimeFormat.ShortDatePattern;
            row++;
        }

        sheet.Cell(row, 1).Value = T("Total");
        sheet.Cell(row, 2).Value = clubEvent.TotalHeadCount;
        for (var i = 0; i < options.Count; i++)
            sheet.Cell(row, 3 + i).Value = options[i].Option.Total;

        sheet.Range(row, 1, row, headers.Count).Style.Font.Bold = true;
        sheet.Range(row, 1, row, headers.Count).Style.Fill.BackgroundColor = HeaderColor;

        FinishSheet(sheet, headers.Count, row, headerRows: 2);
    }

    private void AddSummarySheet(XLWorkbook workbook, ClubEventDto clubEvent)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetSummary"));

        sheet.Cell(1, 1).Value = clubEvent.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = T("Date");
        sheet.Cell(2, 2).Value = FormatDate(clubEvent);
        sheet.Cell(3, 1).Value = T("Location");
        sheet.Cell(3, 2).Value = clubEvent.Location ?? "";
        sheet.Cell(4, 1).Value = T("Registrations");
        sheet.Cell(4, 2).Value = clubEvent.Registrations.Count;
        sheet.Cell(5, 1).Value = T("HeadCount");
        sheet.Cell(5, 2).Value = clubEvent.MaxParticipants.HasValue ? $"{clubEvent.TotalHeadCount} / {clubEvent.MaxParticipants}" : clubEvent.TotalHeadCount.ToString(culture);

        var row = 7;
        WriteHeader(sheet, row, [T("Question"), T("Option"), T("Total")]);
        foreach (var question in clubEvent.Questions)
        {
            foreach (var option in question.Options)
            {
                row++;
                sheet.Cell(row, 1).Value = question.Text;
                sheet.Cell(row, 2).Value = option.Label;
                sheet.Cell(row, 3).Value = option.Total;
            }
        }

        sheet.Columns().AdjustToContents();
    }

    private string FormatDate(ClubEventDto clubEvent)
    {
        var date = clubEvent.StartDate.ToString("d", culture);
        if (clubEvent.EndDate.HasValue)
            date += " – " + clubEvent.EndDate.Value.ToString("d", culture);

        if (clubEvent.StartTime.HasValue)
        {
            date += ", " + clubEvent.StartTime.Value.ToString("t", culture);
            if (clubEvent.EndTime.HasValue)
                date += " – " + clubEvent.EndTime.Value.ToString("t", culture);
        }

        return date;
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, List<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderColor;
        }
    }

    private static void FinishSheet(IXLWorksheet sheet, int columnCount, int lastRow, int headerRows)
    {
        var table = sheet.Range(1, 1, lastRow, columnCount);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.SheetView.FreezeRows(headerRows);
        sheet.Columns().AdjustToContents();
        sheet.PageSetup.PageOrientation = columnCount > 6 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
    }

    private string SheetName(string key)
    {
        var name = T(key);
        return name.Length > 31 ? name[..31] : name;
    }

    private string T(string key) => Texts.GetString(key, culture) ?? key;
}
