using System.Globalization;
using Bookennis.Client.Localization.Pages.Members;
using Bookennis.Shared.Controller.Club;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Members.Components;

public static class MemberAdminHelpers
{
    public static string SeasonLabel(SeasonResult season) => SeasonLabel(season.StartDate, season.EndDate);

    public static string SeasonLabel(DateOnly start, DateOnly end)
        => start.Year == end.Year ? start.Year.ToString(CultureInfo.InvariantCulture) : $"{start.Year}/{end:yy}";

    public static int Age(DateOnly birthday)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthday.Year;
        if (birthday.AddYears(age) > today)
            age--;
        return age;
    }

    public static AgeGroup AgeGroupOf(DateOnly birthday) => Age(birthday) switch
    {
        <= 12 => AgeGroup.Kids,
        <= 17 => AgeGroup.Teenagers,
        <= 34 => AgeGroup.Adults,
        _ => AgeGroup.Seniors,
    };

    public static string RelativeDate(IStringLocalizer<MembersAdminLocale> locale, DateTimeOffset? value)
    {
        if (value is null)
            return locale["NeverPlayed"];

        var days = DateOnly.FromDateTime(DateTime.Today).DayNumber - DateOnly.FromDateTime(value.Value.LocalDateTime).DayNumber;
        return days switch
        {
            <= 0 => locale["Today"],
            1 => locale["Yesterday"],
            < 30 => string.Format(locale["DaysAgo"], days),
            < 60 => locale["MonthAgo"],
            < 335 => string.Format(locale["MonthsAgo"], days / 30),
            < 730 => locale["YearAgo"],
            _ => string.Format(locale["YearsAgo"], days / 365),
        };
    }

    /// <summary>Accent color per role, used for chips.</summary>
    public static string RoleColor(MemberRole role) => role switch
    {
        MemberRole.Admin => "#DC2626",
        MemberRole.SportsDirector => "#7C3AED",
        MemberRole.YouthSportsDirector => "#DB2777",
        MemberRole.Trainer => "#0891B2",
        MemberRole.Treasurer => "#D97706",
        MemberRole.Maintainer => "#4B5563",
        MemberRole.Guest => "#9333EA",
        _ => "#059669",
    };

    public static readonly MemberRole[] AssignableRoles = Enum.GetValues<MemberRole>().Where(r => r != MemberRole.Guest).ToArray();
}
