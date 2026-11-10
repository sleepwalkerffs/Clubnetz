using Bookennis.Api.Business.Permissions;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

/// <summary>Who may book for players without playing themself. One rule for every booking handler.</summary>
internal static class BookingRights
{
    /// <summary>
    /// Whether the user may book for any member of the club: they have the permission <see cref="Permission.BookingsManageForAnyMember"/>
    /// (by default Admin, SportsDirector, YouthSportsDirector and Trainer; the club admin can change that, also per play mode with a grant),
    /// or they are an application administrator without a member of the club (support mode). An administrator who is a member acts with
    /// the roles of the member. The play mode's allowed roles, enrollment, quotas and every other booking rule still apply.
    /// </summary>
    public static async Task<bool> MayBookForAnyMember(AppDbContext context, int clubId, int userId, int playModeId, CancellationToken cancellationToken)
    {
        var member = await context.Set<Member>()
            .Where(m => m.UserId == userId && m.ClubId == clubId)
            .OrderBy(m => m.MemberType) // ClubMember (0) before GuestMember (1)
            .Select(m => new { m.Id, m.MemberType, m.UserRoles })
            .FirstOrDefaultAsync(cancellationToken);

        // A guest member gives no rights
        if (member is not null)
        {
            if (member.MemberType != MemberType.ClubMember)
                return false;

            return await PermissionEvaluator.Has(
                context, clubId, member.Id, member.UserRoles, Permission.BookingsManageForAnyMember,
                PermissionResourceType.PlayMode, playModeId, cancellationToken);
        }

        var administrator = (int)Bookennis.Domain.User.UserRoles.Administrator;
        return await context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == administrator, cancellationToken);
    }
}
