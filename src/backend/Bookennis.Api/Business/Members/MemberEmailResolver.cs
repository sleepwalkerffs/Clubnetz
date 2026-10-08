using Bookennis.Api.Data;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

public record MemberNotificationInfo(string? Email, string FirstName, string LastName, Language Language, int ClubId, string ClubName);

public static class MemberEmailResolver
{
    /// <summary>
    /// Resolves the member's own email, falling back to a parent's email (via FamilyMembers) if the
    /// member has none - e.g. a child member without their own login.
    /// </summary>
    public static async Task<MemberNotificationInfo?> GetNotificationInfo(AppDbContext context, int memberId, CancellationToken cancellationToken)
    {
        var memberInfo = await (
            from member in context.Set<Member>()
            join user in context.Users on member.UserId equals user.Id
            join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id
            where member.Id == memberId
            select new { member.Id, user.Email, user.FirstName, user.LastName, user.Language, ClubId = club.Id, ClubName = club.Name }
        ).SingleOrDefaultAsync(cancellationToken);

        if (memberInfo is null)
            return null;

        var effectiveEmail = memberInfo.Email;
        if (effectiveEmail is null)
        {
            effectiveEmail = await (
                from childFm in context.FamilyMembers.OfType<ChildFamilyMember>()
                where childFm.MemberId == memberInfo.Id
                from parentFm in context.FamilyMembers.OfType<ParentFamilyMember>().Where(pf => pf.ParentFamilyId == childFm.ChildFamilyId)
                join parentMember in context.ClubMembers on parentFm.MemberId equals parentMember.Id
                join parentUser in context.Users on parentMember.UserId equals parentUser.Id
                where parentUser.Email != null
                orderby parentUser.LastName, parentUser.FirstName
                select parentUser.Email
            ).FirstOrDefaultAsync(cancellationToken);
        }

        return new MemberNotificationInfo(effectiveEmail, memberInfo.FirstName, memberInfo.LastName, memberInfo.Language, memberInfo.ClubId, memberInfo.ClubName);
    }
}
