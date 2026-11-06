using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

/// <summary>Who may book for players without playing themself. One rule for every booking handler.</summary>
internal static class BookingRights
{
    /// <summary>
    /// Roles that may book for any member of the club (a sports director enters a league match, a trainer a lesson for somebody's child).
    /// Narrower than <see cref="BookingEditRules.ElevatedRoles"/>: the technical role Maintainer is left out.
    /// The play mode's allowed roles, enrollment, quotas and every other booking rule still apply.
    /// </summary>
    public static readonly MemberRole[] BookForAnyMemberRoles =
    [
        MemberRole.Admin,
        MemberRole.SportsDirector,
        MemberRole.YouthSportsDirector,
        MemberRole.Trainer,
    ];

    public static bool MayBookForAnyMember(IEnumerable<MemberRole>? roles)
        => roles?.Any(r => BookForAnyMemberRoles.Contains(r)) ?? false;

    /// <summary>
    /// Whether the user may book for any member of the club: a staff role in the club, or an application administrator without a member
    /// of the club (support mode). An administrator who is a member acts with the roles of the member.
    /// </summary>
    public static async Task<bool> MayBookForAnyMember(AppDbContext context, int clubId, int userId, CancellationToken cancellationToken)
    {
        var memberRoles = await context.Set<Member>()
            .Where(m => m.UserId == userId && m.ClubId == clubId)
            .OrderBy(m => m.MemberType) // ClubMember (0) before GuestMember (1)
            .Select(m => m.UserRoles)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberRoles is not null)
            return MayBookForAnyMember(memberRoles);

        var administrator = (int)Bookennis.Domain.User.UserRoles.Administrator;
        return await context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == administrator, cancellationToken);
    }
}
