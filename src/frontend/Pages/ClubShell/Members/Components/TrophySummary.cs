using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Client.Pages.ClubShell.Members.Components;

/// <summary>Key figures of a trophy case, shown on the profile and the trophy case page.</summary>
public sealed record TrophySummary(int TotalBadges, int SpecialAwards, int Seasons, int? HighestLevel, EarnedBadgeDto? TopBadge, OneTimeBadgeAwardDto? LatestSpecialAward)
{
    public bool IsEmpty => TotalBadges == 0;

    public static TrophySummary From(GetTrophyCaseResult trophyCase)
    {
        var seasons = trophyCase.Clubs.SelectMany(c => c.Seasons).ToList();
        var badges = seasons.SelectMany(s => s.Badges).ToList();
        var specialAwards = seasons.SelectMany(s => s.OneTimeBadges).ToList();

        return new TrophySummary(
            badges.Count + specialAwards.Count,
            specialAwards.Count,
            seasons.Count(s => s.Badges.Count > 0 || s.OneTimeBadges.Count > 0),
            badges.Count > 0 ? badges.Max(b => b.Level) : null,
            badges.OrderByDescending(b => b.Level).ThenByDescending(b => b.EarnedAt).FirstOrDefault(),
            specialAwards.OrderByDescending(b => b.AwardedAt).FirstOrDefault());
    }
}

public static class BadgeColors
{
    /// <summary>Accent color of a badge: bronze, silver, gold, then platinum and diamond. One-time badges (no level) are pink.</summary>
    public static string ForLevel(int? level) => level switch
    {
        null => "#EC4899",
        <= 1 => "#C2410C",
        2 => "#64748B",
        3 => "#D97706",
        4 => "#0891B2",
        _ => "#7C3AED",
    };
}
