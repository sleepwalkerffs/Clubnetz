using System.Globalization;
using Bookennis.Client.Localization.Pages.Clubhouse;
using Bookennis.Shared.Controller.Clubhouse;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Clubhouse;

/// <summary>Texts, urls and small calculations shared by the clubhouse pages, their dialogs and the card on My Club.</summary>
public static class ClubhouseHelpers
{
    public static readonly ClubhouseItemCategory[] Categories = Enum.GetValues<ClubhouseItemCategory>();
    public static readonly ClubhousePaymentMethod[] PaymentMethods = Enum.GetValues<ClubhousePaymentMethod>();

    /// <summary>The clubhouse host and the admins, same as the backend policy <c>ClubhouseHost</c>.</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles) => roles.Any(r => r is MemberRole.ClubhouseHost or MemberRole.Admin);

    public static string Url(int clubId) => $"/clubs/{clubId}/clubhouse";

    public static string TabUrl(int clubId, int tabId) => $"/clubs/{clubId}/clubhouse/tabs/{tabId}";

    public static string MenuUrl(int clubId) => $"/clubs/{clubId}/clubhouse?view=menu";

    /// <summary>Amount in euros, e.g. "€ 4,50" (only eur is sold).</summary>
    public static string Amount(long cents) => "€ " + (cents / 100m).ToString("N2", CultureInfo.CurrentCulture);

    public static string Price(IStringLocalizer<ClubhouseLocale> locale, int cents) => cents == 0 ? locale["Free"] : Amount(cents);

    public static string CategoryEmoji(ClubhouseItemCategory category) => category switch
    {
        ClubhouseItemCategory.Drink => "🍺",
        ClubhouseItemCategory.Food => "🥪",
        _ => "🍦",
    };

    public static string CategoryText(IStringLocalizer<ClubhouseLocale> locale, ClubhouseItemCategory category) => locale[$"Category_{category}"];

    public static string MethodEmoji(ClubhousePaymentMethod method) => method switch
    {
        ClubhousePaymentMethod.Cash => "💶",
        ClubhousePaymentMethod.Card => "💳",
        _ => "🧾",
    };

    public static string MethodText(IStringLocalizer<ClubhouseLocale> locale, ClubhousePaymentMethod method) => locale[$"Method_{method}"];

    /// <summary>The name of a tab: the member or the guest from outside, "former member" once the member left.</summary>
    public static string TabName(IStringLocalizer<ClubhouseLocale> locale, string name) => string.IsNullOrEmpty(name) ? locale["FormerMember"] : name;

    public static string Day(DateTimeOffset time) => time.ToLocalTime().ToString("ddd, d. MMM", CultureInfo.CurrentCulture);

    public static string Time(DateTimeOffset time) => time.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>
    /// The search of the host: every word must match the start of the first or the last name (like the player search), so
    /// "max mu" and "mu max" both find Max Muster.
    /// </summary>
    public static bool Matches(ClubhouseGuestDto guest, string search)
    {
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Length > 0 && terms.All(term =>
            guest.FirstName.StartsWith(term, StringComparison.CurrentCultureIgnoreCase)
            || guest.LastName.StartsWith(term, StringComparison.CurrentCultureIgnoreCase)
            || guest.LastName.Split(' ', '-').Any(part => part.StartsWith(term, StringComparison.CurrentCultureIgnoreCase)));
    }
}
