using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubApiKeys;

public static class ClubApiKeyQueryExtensions
{
    /// <summary>
    /// The admins of the club. An API key acts as its creator and only works while that user is one of them.
    /// The tenant filter is ignored because a key is checked before the request is known to belong to its club.
    /// </summary>
    public static IQueryable<Member> ClubAdmins(this AppDbContext context, int clubId)
        => context.Set<Member>()
            .IgnoreQueryFilters()
            .Where(m => m.ClubId == clubId && m.MemberType == MemberType.ClubMember && m.UserRoles.Contains(MemberRole.Admin));
}
