using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;

namespace Bookennis.Api.Business.Families.Helpers;

public static class FamilyCreateUpdateHelper
{
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