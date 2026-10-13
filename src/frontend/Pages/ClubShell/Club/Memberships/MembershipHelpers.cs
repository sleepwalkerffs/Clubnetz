using System.Globalization;
using Bookennis.Client.Localization.Pages.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Club.Memberships;

/// <summary>Who one order of a plan covers, as the treasurer chooses it in the plan dialog.</summary>
public enum MembershipCoverage
{
    OneMember,
    Couple,
    FamilyOneAdult,
    FamilyTwoAdults,
}

public static class MembershipHelpers
{
    /// <summary>Roles that design plans and look after the payments, same as the backend policy <c>ClubTreasurer</c>.</summary>
    public static bool CanManage(IEnumerable<MemberRole> roles)
        => roles.Any(r => r is MemberRole.Treasurer or MemberRole.Admin);

    /// <summary>Only admins connect the club with Stripe, same as the backend policy <c>ClubPaymentAccountManager</c>.</summary>
    public static bool CanConnectPayments(IEnumerable<MemberRole> roles) => roles.Contains(MemberRole.Admin);

    public static string PlansUrl(int clubId) => $"/clubs/{clubId}/membership-plans";

    public static string PaymentsUrl(int clubId, int? seasonId = null)
        => seasonId is null ? $"/clubs/{clubId}/membership-payments" : $"/clubs/{clubId}/membership-payments?season={seasonId}";

    public static string MembershipUrl(int clubId, int? seasonId = null)
        => seasonId is null ? $"/clubs/{clubId}/membership" : $"/clubs/{clubId}/membership?season={seasonId}";

    /// <summary>An amount in euro, e.g. "€ 215,00".</summary>
    public static string Amount(long cents) => "€ " + (cents / 100m).ToString("N2", CultureInfo.CurrentCulture);

    public static string Price(IStringLocalizer<MembershipsLocale> locale, int priceCents) => priceCents == 0 ? locale["Free"] : Amount(priceCents);

    public static MembershipCoverage Coverage(int maxAdults, bool includesChildren) => (maxAdults, includesChildren) switch
    {
        (1, false) => MembershipCoverage.OneMember,
        (_, false) => MembershipCoverage.Couple,
        (1, true) => MembershipCoverage.FamilyOneAdult,
        _ => MembershipCoverage.FamilyTwoAdults,
    };

    public static MembershipCoverage Coverage(this MembershipPlanDto plan) => Coverage(plan.MaxAdults, plan.IncludesChildren);

    public static (int MaxAdults, bool IncludesChildren) ToRules(this MembershipCoverage coverage) => coverage switch
    {
        MembershipCoverage.Couple => (2, false),
        MembershipCoverage.FamilyOneAdult => (1, true),
        MembershipCoverage.FamilyTwoAdults => (2, true),
        _ => (1, false),
    };

    public static string CoverageText(IStringLocalizer<MembershipsLocale> locale, MembershipCoverage coverage) => locale[$"Coverage_{coverage}"];

    /// <summary>"born 2009 to 2013", "born 2014 or later", "born 1956 or earlier"; <c>null</c> without an age limit.</summary>
    public static string? BirthYearText(IStringLocalizer<MembershipsLocale> locale, int? from, int? to) => (from, to) switch
    {
        (null, null) => null,
        ({ } f, { } t) when f == t => string.Format(locale["Born_In"], f),
        ({ } f, { } t) => string.Format(locale["Born_Between"], f, t),
        ({ } f, null) => string.Format(locale["Born_From"], f),
        (null, { } t) => string.Format(locale["Born_Until"], t),
    };

    /// <summary>The ages of the birth years in the year of the season, e.g. "13 to 17 years"; <c>null</c> without an age limit.</summary>
    public static string? AgeText(IStringLocalizer<MembershipsLocale> locale, int? birthYearFrom, int? birthYearTo, int seasonYear)
    {
        // The oldest birth year is the highest age
        var maxAge = seasonYear - birthYearFrom;
        var minAge = seasonYear - birthYearTo;

        return (minAge, maxAge) switch
        {
            (null, null) => null,
            ({ } min, { } max) when min == max => string.Format(locale["Age_Exactly"], min),
            ({ } min, { } max) => string.Format(locale["Age_Between"], min, max),
            (null, { } max) => string.Format(locale["Age_UpTo"], max),
            ({ } min, null) => string.Format(locale["Age_From"], min),
        };
    }

    public static string? AvailabilityText(IStringLocalizer<MembershipsLocale> locale, DateOnly? from, DateOnly? until) => (from, until) switch
    {
        (null, null) => null,
        ({ } f, { } u) => string.Format(locale["Available_Between"], Date(f), Date(u)),
        ({ } f, null) => string.Format(locale["Available_From"], Date(f)),
        (null, { } u) => string.Format(locale["Available_Until"], Date(u)),
    };

    /// <summary>The rules of a plan as short chips (emoji and text), most important first.</summary>
    public static List<(string Emoji, string Text)> Chips(IStringLocalizer<MembershipsLocale> locale, MembershipPlanDto plan)
    {
        var chips = new List<(string, string)> { (plan.IsGroupPlan ? "👨‍👩‍👧" : "👤", CoverageText(locale, plan.Coverage())) };

        if (BirthYearText(locale, plan.BirthYearFrom, plan.BirthYearTo) is { } born)
            chips.Add(("🎂", born));

        if (plan.RequiresApproval)
            chips.Add(("✋", locale["Chip_RequiresApproval"]));

        if (plan.NewMembersOnly)
            chips.Add(("🌱", locale["Chip_NewMembersOnly"]));

        if (plan.RequiresEnrolledFamilyAdult)
            chips.Add(("🤝", locale["Chip_FamilyDiscount"]));

        if (!plan.GrantsSeasonEnrollment)
            chips.Add(("🪑", locale["Chip_Passive"]));

        if (AvailabilityText(locale, plan.AvailableFrom, plan.AvailableUntil) is { } available)
            chips.Add(("⏳", available));

        return chips;
    }

    /// <summary>Reads the rules of a plan back as sentences, so the treasurer sees what members will get.</summary>
    public static List<string> Sentences(IStringLocalizer<MembershipsLocale> locale, MembershipPlanDto plan, int seasonYear)
    {
        var sentences = new List<string> { locale[$"Sentence_{plan.Coverage()}"] };

        if (BirthYearText(locale, plan.BirthYearFrom, plan.BirthYearTo) is { } born)
        {
            var who = locale[plan.IsGroupPlan ? "Sentence_AgeOfAdults" : "Sentence_Age"];
            sentences.Add(string.Format(who, born, AgeText(locale, plan.BirthYearFrom, plan.BirthYearTo, seasonYear), seasonYear));
        }

        if (plan.NewMembersOnly)
            sentences.Add(locale["Sentence_NewMembersOnly"]);

        if (plan.RequiresEnrolledFamilyAdult)
            sentences.Add(locale["Sentence_FamilyDiscount"]);

        if (AvailabilityText(locale, plan.AvailableFrom, plan.AvailableUntil) is { } available)
            sentences.Add(string.Format(locale["Sentence_Available"], available));

        if (!plan.GrantsSeasonEnrollment)
            sentences.Add(locale["Sentence_Passive"]);

        sentences.Add(locale[(plan.IsSelfService, plan.RequiresApproval, plan.IsFree) switch
        {
            (false, _, _) => "Sentence_NotSelfService",
            (true, true, true) => "Sentence_ApprovalThenConfirm",
            (true, true, false) => "Sentence_ApprovalThenPay",
            (true, false, true) => "Sentence_Confirm",
            (true, false, false) => "Sentence_Pay",
        }]);

        return sentences;
    }

    public static string StatusText(IStringLocalizer<MembershipsLocale> locale, MembershipOrderStatus status) => locale[$"Status_{status}"];

    /// <summary>The accent color of a status as CSS value. The dark-only tokens keep the text readable on dark surfaces.</summary>
    public static string StatusColor(MembershipOrderStatus status) => status switch
    {
        MembershipOrderStatus.Paid => "var(--bk-dark-green, #047857)",
        MembershipOrderStatus.Processing => "var(--bk-dark-blue, #1D4ED8)",
        MembershipOrderStatus.AwaitingApproval => "var(--bk-dark-violet, #6D28D9)",
        MembershipOrderStatus.AwaitingPayment => "var(--bk-dark-amber, #B45309)",
        MembershipOrderStatus.Failed or MembershipOrderStatus.Disputed => "var(--bk-dark-red, #B91C1C)",
        _ => "var(--mud-palette-text-secondary)",
    };

    public static string StatusEmoji(MembershipOrderStatus status) => status switch
    {
        MembershipOrderStatus.Paid => "✅",
        MembershipOrderStatus.Processing => "🏦",
        MembershipOrderStatus.AwaitingApproval => "✋",
        MembershipOrderStatus.AwaitingPayment => "⏳",
        MembershipOrderStatus.Failed => "⚠️",
        MembershipOrderStatus.Disputed => "⚖️",
        MembershipOrderStatus.Refunded => "↩️",
        _ => "🚫",
    };

    public static string MethodText(IStringLocalizer<MembershipsLocale> locale, MembershipPaymentMethod? method)
        => method is null ? "" : locale[$"Method_{method}"];

    /// <summary>Why a plan can't be chosen, for the error codes of the backend (<c>MembershipEligibility.ErrorCode</c>).</summary>
    public static string ReasonText(IStringLocalizer<MembershipsLocale> locale, string reason)
    {
        var text = locale[$"Reason_{reason}"];
        return text.ResourceNotFound ? locale["Reason_Other"] : text;
    }

    /// <summary>An order holds the membership of its members: it is not cancelled, failed or refunded.</summary>
    /// <summary>The members a payment can be recorded for: those no running order of the season covers.</summary>
    public static List<MembershipPaymentMemberDto> MembersWithoutMembership(GetMembershipPaymentsResult payments)
    {
        var withMembership = payments.Orders.Where(o => o.Status.IsActive()).SelectMany(o => o.MemberIds).ToHashSet();
        return payments.Members.Where(m => !withMembership.Contains(m.MemberId)).ToList();
    }

    /// <summary>
    /// Who the treasurer can put on an existing order: the payer, the members it already covers and the rest of the payer's family,
    /// as far as they have no other membership in the season.
    /// </summary>
    public static List<MembershipPaymentMemberDto> CoverableBy(GetMembershipPaymentsResult payments, MembershipPaymentOrderDto order, MembershipPaymentMemberDto payer)
    {
        var free = MembersWithoutMembership(payments).Select(m => m.MemberId).ToHashSet();
        return payments.Members
            .Where(m => order.MemberIds.Contains(m.MemberId)
                     || (free.Contains(m.MemberId) && (m.MemberId == payer.MemberId || (payer.FamilyId is not null && m.FamilyId == payer.FamilyId))))
            .OrderBy(m => m.MemberId == payer.MemberId ? 0 : 1).ThenBy(m => m.IsChild).ThenBy(m => m.FirstName)
            .ToList();
    }

    /// <summary>Who a couple or family plan covers unless the treasurer says otherwise: the family as far as the plan allows.</summary>
    public static List<MembershipPaymentMemberDto> CoveredByDefault(MembershipPlanDto plan, IReadOnlyCollection<MembershipPaymentMemberDto> family)
    {
        var adults = family.Where(m => !m.IsChild).Take(plan.MaxAdults);
        var children = plan.IncludesChildren ? family.Where(m => m.IsChild) : [];
        return adults.Concat(children).ToList();
    }

    public static bool IsActive(this MembershipOrderStatus status)
        => status is not (MembershipOrderStatus.Cancelled or MembershipOrderStatus.Failed or MembershipOrderStatus.Refunded);

    public static string Date(DateOnly date) => date.ToString("d", CultureInfo.CurrentCulture);

    public static string Date(DateTimeOffset date) => date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
}
