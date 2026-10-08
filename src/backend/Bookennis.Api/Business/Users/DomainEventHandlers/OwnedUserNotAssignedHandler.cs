using Bookennis.Api.Business.Events;
using Bookennis.Api.Data;
using Bookennis.Domain.Families.Events;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Users.DomainEventHandlers;

public class OwnedUserNotAssignedHandler(AppDbContext context) : INotificationHandler<DomainEvent<ChildrenRemovedDomainEvent>>
{
    public async Task Handle(DomainEvent<ChildrenRemovedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var removedChildrenMemberIds = notification.Event.ChildMemberIds;

        if (removedChildrenMemberIds.Count == 0)
            return;

        var usersToRemove = await (from member in context.ClubMembers
                                   join user in context.Users on member.UserId equals user.Id

                                   let hasFamily = context.FamilyMembers.Any(f => f.MemberId == member.Id)

                                   where removedChildrenMemberIds.Contains(member.Id)
                                      && user.BelongsToUserId != null
                                      && !hasFamily

                                   select new { User = user, Member = member }
                                  ).ToListAsync(cancellationToken);

        foreach (var users in usersToRemove)
        {
            context.Remove(users.Member);
            context.Remove(users.User);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}