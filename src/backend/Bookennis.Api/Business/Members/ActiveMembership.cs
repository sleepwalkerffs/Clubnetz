using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>
/// Who is an active member of a club: the one place that decides it (policy <c>ActiveMember</c>, the anonymized calendar,
/// <c>GetClubProfile</c>, <c>GetBookingOptions</c>, court sales).
/// <list type="bullet">
/// <item>a <see cref="ClubMember"/> enrolled in the active season (the season whose period contains today),</item>
/// <item>a parent of a family with a child enrolled in it (they book for their children),</item>
/// <item>a member with a role other than <see cref="MemberRole.User"/>/<see cref="MemberRole.Guest"/> (staff).</item>
/// </list>
/// Application administrators (support mode) are active everywhere; the callers check that themselves because they have no member.
/// Everybody else in the club only sees an anonymized calendar and can buy court time.
/// </summary>
public static class ActiveMembership
{
    public static readonly MemberRole[] StaffRoles =
    [
        MemberRole.Admin, MemberRole.Maintainer, MemberRole.Treasurer, MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Trainer,
    ];

    public static bool IsStaff(IEnumerable<MemberRole> roles) => roles.Any(StaffRoles.Contains);

    /// <summary>The season of the club whose period contains today (UTC date, like the booking rules).</summary>
    public static Task<Season?> GetActiveSeason(this AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return context.Seasons
            .Where(s => s.ClubId == clubId && s.Period.From <= today && today <= s.Period.To)
            .OrderByDescending(s => s.Period.From)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The member is enrolled in the active season of the club itself (not through a child).</summary>
    public static async Task<bool> IsEnrolledInActiveSeason(AppDbContext context, int clubId, int memberId, CancellationToken cancellationToken)
    {
        var season = await context.GetActiveSeason(clubId, cancellationToken);
        return season is not null && await context.MemberSeasons.AnyAsync(ms => ms.MemberId == memberId && ms.SeasonId == season.Id, cancellationToken);
    }

    /// <summary>See <see cref="ActiveMembership"/>. <c>false</c> for a member of another club or a member that does not exist.</summary>
    public static async Task<bool> IsActive(AppDbContext context, int clubId, int memberId, CancellationToken cancellationToken)
    {
        var roles = await context.ClubMembers
            .Where(m => m.Id == memberId && m.ClubId == clubId)
            .Select(m => m.UserRoles)
            .FirstOrDefaultAsync(cancellationToken);

        if (roles is null)
            return false;

        if (IsStaff(roles))
            return true;

        var season = await context.GetActiveSeason(clubId, cancellationToken);
        if (season is null)
            return false;

        if (await context.MemberSeasons.AnyAsync(ms => ms.MemberId == memberId && ms.SeasonId == season.Id, cancellationToken))
            return true;

        // A parent books for an enrolled child
        return await context.Families.AnyAsync(
            family => family.ClubId == clubId
                && family.Parents.Any(parent => parent.MemberId == memberId)
                && family.Children.Any(child => context.MemberSeasons.Any(ms => ms.MemberId == child.MemberId && ms.SeasonId == season.Id)),
            cancellationToken);
    }

    /// <summary>
    /// The members whose bookings a member who is not active sees in full: themself and the children of the families they are a parent of.
    /// </summary>
    public static async Task<HashSet<int>> OwnAndChildMemberIds(AppDbContext context, int clubId, int userId, CancellationToken cancellationToken)
    {
        var memberIds = await context.ClubMembers
            .Where(m => m.UserId == userId && m.ClubId == clubId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (memberIds.Count == 0)
            return [];

        var childIds = await context.Families
            .Where(family => family.ClubId == clubId && family.Parents.Any(parent => memberIds.Contains(parent.MemberId)))
            .SelectMany(family => family.Children.Select(child => child.MemberId))
            .ToListAsync(cancellationToken);

        return memberIds.Concat(childIds).ToHashSet();
    }
}
