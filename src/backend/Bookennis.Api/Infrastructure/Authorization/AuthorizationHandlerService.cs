using System.Security.Claims;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization;

public class AuthorizationHandlerService(AppDbContext context)
{
    /// <summary>
    /// The <see cref="ClubMember"/> of the user in the club. A former guest may still have a <see cref="GuestMember"/> (historic bookings),
    /// which gives no rights since the guest system was removed. <paramref name="principal"/> is no longer used.
    /// </summary>
    public async Task<(int? MemberId, MemberRole[]? Role)> GetMember(int userId, int clubId, ClaimsPrincipal? principal = null)
    {
        var result = await (from member in context.Set<Member>()
                            where member.UserId == userId && member.ClubId == clubId && member.MemberType == MemberType.ClubMember
                            orderby member.Id
                            select member).FirstOrDefaultAsync();

        return (result?.Id, result?.UserRoles);
    }

    public async Task<List<int>?> GetChildMemberIds(int memberId)
    {
        var childMemberIds = await (from family in context.Families
                                    from child in family.Children
                                    where family.FamilyMembers.Any(i => i.MemberId == memberId)
                                    select child.MemberId).ToListAsync();
        return childMemberIds;

    }

    public async Task<List<PlayMode>> GetPlayModes(int clubId)
        => await context.PlayModes.Where(playMode => playMode.ClubId == clubId).ToListAsync();
}
