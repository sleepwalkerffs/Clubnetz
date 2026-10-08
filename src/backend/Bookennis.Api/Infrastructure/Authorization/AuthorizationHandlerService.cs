using System.Security.Claims;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization;

public class AuthorizationHandlerService(AppDbContext context)
{
    public async Task<(int? MemberId, MemberRole[]? Role)> GetMember(int userId, int clubId, ClaimsPrincipal? principal = null)
    {
        var isGuestSession = principal?.FindFirst("guest_session")?.Value == "true";

        var query = from member in context.Set<Member>()
                    where member.UserId == userId && member.ClubId == clubId
                    select member;

        query = isGuestSession
            ? query.Where(m => m.MemberType == MemberType.GuestMember)
            : query.Where(m => m.MemberType == MemberType.ClubMember);

        var result = await query.SingleOrDefaultAsync();

        // Fallback: if no member found with the specific type, try any member
        result ??= await (from member in context.Set<Member>()
                          where member.UserId == userId && member.ClubId == clubId
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
