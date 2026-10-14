using System.Globalization;
using System.Resources;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Shared.Controller.Memberships;
using ClosedXML.Excel;

namespace Bookennis.Api.Business.Memberships;

/// <summary>The payments list of a season as an Excel workbook, in the language of the request.</summary>
public record ExportMembershipPayments(int ClubId, int SeasonId) : IQuery<ExportMembershipPaymentsResult>
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor) : IRequestHandler<ExportMembershipPayments, ExportMembershipPaymentsResult>
    {
        public async Task<ExportMembershipPaymentsResult> Handle(ExportMembershipPayments request, CancellationToken cancellationToken)
        {
            var overview = await MembershipPaymentsOverview.Load(context, request.ClubId, request.SeasonId, cancellationToken);
            var exporter = new MembershipPaymentsExcelExporter(languageAccessor.GetCultureInfo());

            return new ExportMembershipPaymentsResult(exporter.Export(overview), $"{exporter.FileName} {overview.Season.Period.From.Year}.xlsx");
        }
    }
}

public record ExportMembershipPaymentsResult(byte[] Data, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>
/// Two sheets: one row per member (who has which plan and whether it is paid) and one row per order (the money).
/// The amount of an order that covers several members is only written in the row of its payer, so the column can be summed up.
/// </summary>
public class MembershipPaymentsExcelExporter(CultureInfo culture)
{
    private static readonly ResourceManager Texts = new("Bookennis.Api.Resources.Localization.Export.MembershipPayments", typeof(MembershipPaymentsExcelExporter).Assembly);
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#E2E8F0");
    private const string AmountFormat = "#,##0.00";

    public string FileName => T("FileName");

    public byte[] Export(MembershipPaymentsOverview overview)
    {
        using var workbook = new XLWorkbook();

        AddMembersSheet(workbook, overview);
        AddOrdersSheet(workbook, overview);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddMembersSheet(XLWorkbook workbook, MembershipPaymentsOverview overview)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetMembers"));
        var headers = new List<string> { T("LastName"), T("FirstName"), T("Enrolled"), T("Plan"), T("Status"), T("Amount"), T("Method"), T("PaidOn"), T("PaidBy") };
        WriteHeader(sheet, headers);

        var names = overview.Members.ToDictionary(m => m.MemberId, m => $"{m.FirstName} {m.LastName}");

        // The order that counts for a member: the one that holds the membership, else the latest one
        var orderByMember = overview.Orders
            .SelectMany(o => o.MemberIds.Select(memberId => (MemberId: memberId, Order: o)))
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => IsActive(x.Order.Status) ? 0 : 1).ThenByDescending(x => x.Order.Id).First().Order);

        var row = 1;
        foreach (var member in overview.Members.OrderBy(m => m.LastName, StringComparer.Create(culture, true)).ThenBy(m => m.FirstName, StringComparer.Create(culture, true)))
        {
            row++;
            sheet.Cell(row, 1).Value = member.LastName;
            sheet.Cell(row, 2).Value = member.FirstName;
            sheet.Cell(row, 3).Value = T($"Enrollment_{member.Enrollment}");

            if (!orderByMember.TryGetValue(member.MemberId, out var order))
                continue;

            sheet.Cell(row, 4).Value = order.PlanName;
            sheet.Cell(row, 5).Value = T($"Status_{order.Status}");

            var carriesAmount = (order.PayerMemberId is { } payerId && order.MemberIds.Contains(payerId) ? payerId : order.MemberIds.Min()) == member.MemberId;
            if (carriesAmount)
            {
                sheet.Cell(row, 6).Value = order.AmountCents / 100m;
                sheet.Cell(row, 6).Style.NumberFormat.Format = AmountFormat;
            }

            sheet.Cell(row, 7).Value = order.PaymentMethod is { } method ? T($"Method_{method}") : "";
            WriteDate(sheet.Cell(row, 8), order.PaidAt);
            sheet.Cell(row, 9).Value = order.PayerMemberId is { } payer ? names.GetValueOrDefault(payer, "") : "";
        }

        FinishSheet(sheet, headers.Count, row);
    }

    private void AddOrdersSheet(XLWorkbook workbook, MembershipPaymentsOverview overview)
    {
        var sheet = workbook.Worksheets.Add(SheetName("SheetOrders"));
        var headers = new List<string> { T("Plan"), T("Members"), T("PaidBy"), T("Amount"), T("Status"), T("Method"), T("OrderedOn"), T("PaidOn"), T("Note") };
        WriteHeader(sheet, headers);

        var names = overview.Members.ToDictionary(m => m.MemberId, m => $"{m.FirstName} {m.LastName}");

        var row = 1;
        foreach (var order in overview.Orders)
        {
            row++;
            sheet.Cell(row, 1).Value = order.PlanName;
            sheet.Cell(row, 2).Value = string.Join(", ", order.MemberIds.Where(names.ContainsKey).Select(id => names[id]));
            sheet.Cell(row, 3).Value = order.PayerMemberId is { } payer ? names.GetValueOrDefault(payer, "") : "";
            sheet.Cell(row, 4).Value = order.AmountCents / 100m;
            sheet.Cell(row, 4).Style.NumberFormat.Format = AmountFormat;
            sheet.Cell(row, 5).Value = T($"Status_{order.Status}");
            sheet.Cell(row, 6).Value = order.PaymentMethod is { } method ? T($"Method_{method}") : "";
            WriteDate(sheet.Cell(row, 7), order.CreatedAt);
            WriteDate(sheet.Cell(row, 8), order.PaidAt);
            sheet.Cell(row, 9).Value = order.Note ?? "";
        }

        FinishSheet(sheet, headers.Count, row);
    }

    private static bool IsActive(MembershipOrderStatus status)
        => status is not (MembershipOrderStatus.Cancelled or MembershipOrderStatus.Failed or MembershipOrderStatus.Refunded);

    // Date only: the time zone of the club is unknown here
    private void WriteDate(IXLCell cell, DateTimeOffset? date)
    {
        if (date is null)
            return;

        cell.Value = date.Value.UtcDateTime.Date;
        cell.Style.DateFormat.Format = culture.DateTimeFormat.ShortDatePattern;
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

    // Sheet names are limited to 31 characters and must not contain some special characters
    private string SheetName(string key)
    {
        var name = T(key);
        return name.Length > 31 ? name[..31] : name;
    }

    private string T(string key) => Texts.GetString(key, culture) ?? key;
}
