using System.Runtime.InteropServices;

namespace Bookennis.Domain.Members;

[Guid("532C08C1-EC1D-4287-B944-4939F26042F6")]
public class ClubMember(int userId, int clubId, MemberRole[] userRoles)
    : Member(userId, clubId, userRoles, MemberType.ClubMember)
{
    public override bool CanParticipate => false;
}