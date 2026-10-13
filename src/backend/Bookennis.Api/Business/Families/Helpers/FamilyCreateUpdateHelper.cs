using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families.Helpers;

public static class FamilyCreateUpdateHelper
{
    /// <summary>
    /// The account of a new child belongs to a parent: to the caller when they are one of the parents, otherwise
    /// (an admin or a treasurer sets up the family) to the first parent.
    /// </summary>
    public static async Task<int> GetOwnerOfNewChildren(AppDbContext context, IReadOnlyCollection<int> parentMemberIds, int callerUserId, CancellationToken cancellationToken)
    {
        var parentUserIds = await context.ClubMembers
            .Where(member => parentMemberIds.Contains(member.Id))
            .OrderBy(member => member.Id)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);

        return parentUserIds.Count == 0 || parentUserIds.Contains(callerUserId) ? callerUserId : parentUserIds[0];
    }

    public static async Task<List<int>> AddNewChildUsers(AppDbContext context, int clubId, List<ChildUserModel> newChildUsers, int userId, CancellationToken cancellationToken)
    {
        var users = newChildUsers.ConvertAll(u => new User(u.FirstName, u.LastName, u.Birthday, u.Gender, userId));
        context.AddRange(users);

        await context.SaveChangesAsync(cancellationToken);

        var members = users.ConvertAll(u => new ClubMember(u.Id, clubId, [MemberRole.User]));
        context.AddRange(members);

        await context.SaveChangesAsync(cancellationToken);

        return members.ConvertAll(cu => cu.Id);
    }
}