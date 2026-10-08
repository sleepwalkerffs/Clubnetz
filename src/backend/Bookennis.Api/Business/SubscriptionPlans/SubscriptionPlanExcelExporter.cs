using System.Globalization;
using System.Resources;
using Bookennis.Shared.Controller.SubscriptionPlans;
using ClosedXML.Excel;

namespace Bookennis.Api.Business.SubscriptionPlans;

/// <summary>Renders a subscription plan as a color-coded Excel workbook (schedule, overview matrix, statistics).</summary>
public class SubscriptionPlanExcelExporter(CultureInfo culture)
{
    private static readonly ResourceManager Texts = new("Bookennis.Api.Resources.Localization.Export.SubscriptionPlan", typeof(SubscriptionPlanExcelExporter).Assembly);

    private static readonly XLColor HeaderColor = XLColor.FromHtml("#E2E8F0");
    private static readonly XLColor ExcludedColor = XLColor.FromHtml("#CBD5E1");
    private static readonly XLColor UnavailableColor = XLColor.FromHtml("#F1F5F9");
    private static readonly XLColor MutedTextColor = XLColor.FromHtml("#64748B");

    public byte[] Export(SubscriptionPlanDto plan)
    {
        using var workbook = new XLWorkbook();
        var participants = plan.Participants.ToDictionary(p => p.Id);

        AddScheduleSheet(workbook, plan, participants);
        AddOverviewSheet(workbook, plan);
        AddStatisticsSheet(workbook, plan, participants);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddScheduleSheet(XLWorkbook workbook, SubscriptionPlanDto plan, Dictionary<int, SubscriptionParticipantDto> participants)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetSchedule"));

        var headers = new List<string> { T("Week"), T("Dates") };
        headers.AddRange(Enumerable.Range(1, plan.PlayersPerWeek).Select(i => string.Format(culture, T("Player"), i)));
        WriteHeader(sheet, 1, headers);

        var row = 2;
        foreach (var week in plan.Weeks)
        {
            WriteWeekCells(sheet, row, week);

            if (week.IsExcluded)
            {
                var range = sheet.Range(row, 1, row, headers.Count);
                range.Style.Fill.BackgroundColor = ExcludedColor;
                sheet.Cell(row, 3).Value = T("NoPlay");
                sheet.Cell(row, 3).Style.Font.Italic = true;
            }
            else
            {
                for (var slot = 0; slot < plan.PlayersPerWeek; slot++)
                {
                    var cell = sheet.Cell(row, 3 + slot);
                    if (slot < week.ParticipantIds.Count && participants.TryGetValue(week.ParticipantIds[slot], out var participant))
                    {
                        cell.Value = participant.Name;
                        ApplyParticipantColor(cell, participant);
                    }
                    else if (week.IsUnderstaffed)
                    {
                        cell.Value = T("Understaffed");
                        cell.Style.Font.Italic = true;
                        cell.Style.Font.FontColor = MutedTextColor;
                    }
                }
            }

            row++;
        }

        FinishSheet(sheet, headers.Count, row - 1);
    }

    private void AddOverviewSheet(XLWorkbook workbook, SubscriptionPlanDto plan)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetOverview"));

        var headers = new List<string> { T("Week"), T("Dates") };
        headers.AddRange(plan.Participants.Select(p => p.Name));
        WriteHeader(sheet, 1, headers);
        for (var i = 0; i < plan.Participants.Count; i++)
        {
            var cell = sheet.Cell(1, 3 + i);
            ApplyParticipantColor(cell, plan.Participants[i]);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var row = 2;
        foreach (var week in plan.Weeks)
        {
            WriteWeekCells(sheet, row, week);

            if (week.IsExcluded)
            {
                sheet.Range(row, 1, row, headers.Count).Style.Fill.BackgroundColor = ExcludedColor;
            }
            else
            {
                for (var i = 0; i < plan.Participants.Count; i++)
                {
                    var participant = plan.Participants[i];
                    var cell = sheet.Cell(row, 3 + i);
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    if (week.ParticipantIds.Contains(participant.Id))
                    {
                        cell.Value = "✓";
                        ApplyParticipantColor(cell, participant);
                    }
                    else if (participant.UnavailableWeeks.Contains(week.Monday))
                    {
                        cell.Value = "–";
                        cell.Style.Fill.BackgroundColor = UnavailableColor;
                        cell.Style.Font.FontColor = MutedTextColor;
                    }
                }
            }

            row++;
        }

        sheet.Cell(row, 1).Value = T("Total");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        for (var i = 0; i < plan.Participants.Count; i++)
        {
            var cell = sheet.Cell(row, 3 + i);
            cell.Value = plan.Participants[i].Assigned;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        FinishSheet(sheet, headers.Count, row);
    }

    private void AddStatisticsSheet(XLWorkbook workbook, SubscriptionPlanDto plan, Dictionary<int, SubscriptionParticipantDto> participants)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetStatistics"));

        sheet.Cell(1, 1).Value = plan.Name;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = T("Period");
        sheet.Cell(2, 2).Value = $"{FormatDate(plan.StartDate)} – {FormatDate(plan.EndDate)}";
        sheet.Cell(3, 1).Value = T("PlayersPerWeek");
        sheet.Cell(3, 2).Value = plan.PlayersPerWeek;
        sheet.Cell(3, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        var row = 5;
        WriteHeader(sheet, row, [T("Participant"), T("Share"), T("Games"), T("Target")]);
        foreach (var participant in plan.Participants)
        {
            row++;
            ApplyParticipantColor(sheet.Cell(row, 1), participant);
            sheet.Cell(row, 1).Value = participant.Name;
            sheet.Cell(row, 2).Value = participant.Percentage / 100.0;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, 3).Value = participant.Assigned;
            sheet.Cell(row, 4).Value = participant.Target;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "0.0";
        }

        row += 2;
        sheet.Cell(row, 1).Value = T("PairMatrix");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var headerRow = row;
        sheet.Cell(headerRow, 1).Style.Fill.BackgroundColor = HeaderColor;
        for (var i = 0; i < plan.Participants.Count; i++)
        {
            var header = sheet.Cell(headerRow, 2 + i);
            header.Value = plan.Participants[i].Name;
            ApplyParticipantColor(header, plan.Participants[i]);
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var pairCounts = plan.Pairs.ToDictionary(p => (p.ParticipantId1, p.ParticipantId2), p => p.Count);
        foreach (var first in plan.Participants)
        {
            row++;
            ApplyParticipantColor(sheet.Cell(row, 1), first);
            sheet.Cell(row, 1).Value = first.Name;
            for (var i = 0; i < plan.Participants.Count; i++)
            {
                var second = plan.Participants[i];
                var cell = sheet.Cell(row, 2 + i);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if (first.Id == second.Id)
                {
                    cell.Style.Fill.BackgroundColor = ExcludedColor;
                    continue;
                }

                var key = first.Id < second.Id ? (first.Id, second.Id) : (second.Id, first.Id);
                cell.Value = pairCounts.GetValueOrDefault(key);
            }
        }

        sheet.Range(headerRow, 1, row, 1 + participants.Count).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(headerRow, 1, row, 1 + participants.Count).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Columns().AdjustToContents();
        sheet.PageSetup.FitToPages(1, 0);
    }

    private void WriteWeekCells(IXLWorksheet sheet, int row, SubscriptionWeekDto week)
    {
        sheet.Cell(row, 1).Value = week.WeekNumber;
        sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Cell(row, 2).Value = $"{FormatDate(week.Monday)} – {FormatDate(week.Sunday)}";
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

    private static void FinishSheet(IXLWorksheet sheet, int columnCount, int lastRow)
    {
        var table = sheet.Range(1, 1, lastRow, columnCount);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.PageSetup.PageOrientation = columnCount > 6 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
    }

    private static void ApplyParticipantColor(IXLCell cell, SubscriptionParticipantDto participant)
    {
        var color = SubscriptionPlanColors.Get(participant.ColorIndex);
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(color.Background);
        cell.Style.Font.FontColor = XLColor.FromHtml(color.Text);
        cell.Style.Font.Bold = true;
    }

    private string FormatDate(DateOnly date) => date.ToString("d", culture);

    /// <summary>Excel sheet names are limited to 31 characters.</summary>
    private string SheetName(string key)
    {
        var name = T(key);
        return name.Length > 31 ? name[..31] : name;
    }

    private string T(string key) => Texts.GetString(key, culture) ?? key;
}
