using System.Globalization;
using Bookennis.Client.Localization.Pages.CourtSales;
using Bookennis.Shared.Controller.Club;
using Bookennis.Shared.Controller.CourtSales;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Club.CourtSales;

/// <summary>Texts, colors and small calculations shared by the court sales page, its dialogs and the booking page.</summary>
public static class CourtSalesHelpers
{
    /// <summary>Monday first, as people read a week.</summary>
    public static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>Treasurers and admins manage the court sales (same as the backend policy <c>CourtSalesManager</c>).</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles) => roles.Any(r => r is MemberRole.Admin or MemberRole.Treasurer);

    public static string Url(int clubId) => $"/clubs/{clubId}/court-sales";

    /// <summary>Amount in euros, e.g. "€ 28,00" (only eur is sold).</summary>
    public static string Amount(long cents) => "€ " + (cents / 100m).ToString("N2", CultureInfo.CurrentCulture);

    public static string PerHour(IStringLocalizer<CourtSalesLocale> locale, int cents)
        => cents == 0 ? locale["Free"] : string.Format(locale["PricePerHour"], Amount(cents));

    /// <summary>A local time of a price rule; 24:00 is midnight at the end of the day.</summary>
    public static string Time(TimeSpan time) => time >= TimeSpan.FromDays(1) ? "24:00" : time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset time) => time.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    public static string Day(DateTimeOffset time) => time.ToLocalTime().ToString("ddd, d. MMM", CultureInfo.CurrentCulture);

    public static string ShortWeekday(DayOfWeek day) => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day);

    /// <summary>"Mon–Fri", "Sat, Sun" or "Mon, Wed, Fri": runs of three or more days are joined with a dash.</summary>
    public static string WeekdaysText(IStringLocalizer<CourtSalesLocale> locale, IReadOnlyCollection<DayOfWeek> days)
    {
        if (days.Count == 7)
            return locale["EveryDay"];

        var indexes = Week.Select((d, i) => (d, i)).Where(x => days.Contains(x.d)).Select(x => x.i).ToList();
        var parts = new List<string>();
        var start = 0;
        while (start < indexes.Count)
        {
            var end = start;
            while (end + 1 < indexes.Count && indexes[end + 1] == indexes[end] + 1)
                end++;

            if (end - start >= 2)
                parts.Add($"{ShortWeekday(Week[indexes[start]])}–{ShortWeekday(Week[indexes[end]])}");
            else
                parts.AddRange(indexes.Skip(start).Take(end - start + 1).Select(i => ShortWeekday(Week[i])));

            start = end + 1;
        }

        return string.Join(", ", parts);
    }

    public static string CourtsText(IStringLocalizer<CourtSalesLocale> locale, IReadOnlyCollection<int> courtIds, IReadOnlyCollection<CourtResult> courts)
        => courtIds.Count == 0
            ? locale["AllCourts"]
            : string.Join(", ", courts.Where(c => courtIds.Contains(c.CourtId)).Select(c => c.Name));

    public static string StatusText(IStringLocalizer<CourtSalesLocale> locale, CourtPurchaseStatus status) => locale[$"Status_{status}"];

    public static string StatusColor(CourtPurchaseStatus status) => status switch
    {
        CourtPurchaseStatus.Paid => "#059669",
        CourtPurchaseStatus.Processing => "#2563EB",
        CourtPurchaseStatus.AwaitingPayment => "#D97706",
        CourtPurchaseStatus.Failed or CourtPurchaseStatus.Disputed or CourtPurchaseStatus.Refunded => "#DC2626",
        _ => "var(--mud-palette-text-secondary)",
    };

    public static string StatusEmoji(CourtPurchaseStatus status) => status switch
    {
        CourtPurchaseStatus.Paid => "✅",
        CourtPurchaseStatus.Processing => "🏦",
        CourtPurchaseStatus.AwaitingPayment => "⏳",
        CourtPurchaseStatus.Expired => "⌛",
        CourtPurchaseStatus.Failed => "⚠️",
        CourtPurchaseStatus.Cancelled => "🚫",
        CourtPurchaseStatus.Refunded => "↩️",
        CourtPurchaseStatus.Disputed => "⚖️",
        _ => "•",
    };

    /// <summary>How a purchase was paid: "1 credit", "€ 8,00 online", "1 credit + € 8,00 online", "free".</summary>
    public static string PaidText(IStringLocalizer<CourtSalesLocale> locale, bool creditUsed, int amountDueCents, CourtPurchasePaymentMethod? method)
    {
        if (method == CourtPurchasePaymentMethod.Free || (!creditUsed && amountDueCents == 0))
            return locale["Paid_Free"];
        if (creditUsed && amountDueCents > 0)
            return string.Format(locale["Paid_CreditAndOnline"], Amount(amountDueCents));
        if (creditUsed)
            return locale["Paid_Credit"];

        return string.Format(locale["Paid_Online"], Amount(amountDueCents));
    }

    /// <summary>The price rules sorted like a week: by their first weekday, then by time.</summary>
    public static List<CourtPriceRuleDto> Sorted(IEnumerable<CourtPriceRuleDto> rules)
        => rules.OrderBy(r => r.Weekdays.Count == 0 ? 7 : r.Weekdays.Min(d => Array.IndexOf(Week, d))).ThenBy(r => r.From).ThenBy(r => r.CourtIds.Count).ToList();
}
