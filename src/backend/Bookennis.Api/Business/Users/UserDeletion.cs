using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Users;

/// <summary>
/// Deletes a user with all personal data (used by the admin and by the user themselves, GDPR Art. 17).
/// Memberships, bookings players, badges, event registrations, subscription plans, the profile picture and the identity data are removed by
/// the database cascades. Children the user created are handed over to another parent or deleted as well, see
/// <see cref="MemberRemoval"/>.
/// </summary>
public static class UserDeletion
{
    public static async Task DeleteUser(AppDbContext context, User user, CancellationToken cancellationToken)
    {
        var childrenToDelete = await MemberRemoval.HandOverOrCollectOwnedChildren(context, user.Id, cancellationToken);
        var userIds = childrenToDelete.Select(c => c.Id).Append(user.Id).ToList();

        var memberIds = await context.Set<Member>()
            .IgnoreQueryFilters()
            .Where(m => userIds.Contains(m.UserId))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        await MemberRemoval.PrepareRemoval(context, memberIds, cancellationToken);

        context.Users.RemoveRange(childrenToDelete);
        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
