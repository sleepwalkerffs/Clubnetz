using Bookennis.Api.Business.Events;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.Members.Events;
using Bookennis.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push.DomainEventHandlers;

/// <summary>Push notification for a member who earned a tier badge or was awarded a one-time badge.</summary>
public class NotifyBadgeAwardedHandler(AppDbContext context, IPushNotificationService pushNotificationService)
    : INotificationHandler<DomainEvent<MemberBadgeAwardedDomainEvent>>,
        INotificationHandler<DomainEvent<MemberOneTimeBadgeAwardedDomainEvent>>
{
    public async Task Handle(DomainEvent<MemberBadgeAwardedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var badgeName = await context.BadgeTiers
            .IgnoreQueryFilters()
            .Where(t => t.Id == notification.Event.BadgeTierId)
            .Select(t => t.Name)
            .SingleOrDefaultAsync(cancellationToken);

        await Notify(notification.Event.MemberId, badgeName, cancellationToken);
    }

    public async Task Handle(DomainEvent<MemberOneTimeBadgeAwardedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var badgeName = await context.OneTimeBadges
            .IgnoreQueryFilters()
            .Where(b => b.Id == notification.Event.OneTimeBadgeId)
            .Select(b => b.Name)
            .SingleOrDefaultAsync(cancellationToken);

        await Notify(notification.Event.MemberId, badgeName, cancellationToken);
    }

    private async Task Notify(int memberId, string? badgeName, CancellationToken cancellationToken)
    {
        if (badgeName is null)
            return;

        var member = await (
            from m in context.Set<Member>().IgnoreQueryFilters()
            join user in context.Users on m.UserId equals user.Id
            where m.Id == memberId
            select new { m.ClubId, user.FirstName, UserId = user.Id }
        ).SingleOrDefaultAsync(cancellationToken);

        if (member is null)
            return;

        var recipients = await PushRecipients.ForMembers(context, NotificationType.BadgeAwarded, [memberId], exceptUserId: null, cancellationToken);

        // A child's badge goes to the parent's device and names the child
        var isOwnBadge = recipients.Any(r => r.UserId == member.UserId);

        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "BadgeAwarded_Title"),
                isOwnBadge
                    ? PushTexts.Get(culture, "BadgeAwarded_Body", badgeName)
                    : PushTexts.Get(culture, "BadgeAwarded_Body_Child", member.FirstName, badgeName),
                $"/clubs/{member.ClubId}/members/{memberId}/trophy-case"),
            cancellationToken);
    }
}
