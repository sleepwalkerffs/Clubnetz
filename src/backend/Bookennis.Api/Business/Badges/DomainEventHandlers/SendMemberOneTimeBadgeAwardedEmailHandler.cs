using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members.Events;
using Bookennis.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges.DomainEventHandlers;

public class SendMemberOneTimeBadgeAwardedEmailHandler(
    AppDbContext context,
    IMediator mediator,
    AppSettings appSettings) : INotificationHandler<DomainEvent<MemberOneTimeBadgeAwardedDomainEvent>>
{
    public async Task Handle(DomainEvent<MemberOneTimeBadgeAwardedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var memberId = notification.Event.MemberId;
        var oneTimeBadgeId = notification.Event.OneTimeBadgeId;

        // The member's own email or a parent's, unless they don't want badge emails
        var notificationInfo = (await EmailRecipients.ForMembers(context, NotificationType.BadgeAwarded, [memberId], exceptUserIds: null, cancellationToken)).FirstOrDefault();
        if (notificationInfo is null)
            return;

        var oneTimeBadge = await context.OneTimeBadges
            .Where(b => b.Id == oneTimeBadgeId)
            .Select(b => new { b.Name, b.Description, b.ClubId })
            .SingleOrDefaultAsync(cancellationToken);

        if (oneTimeBadge is null)
            return;

        var hasImage = await context.OneTimeBadgeImages.AnyAsync(i => i.OneTimeBadgeId == oneTimeBadgeId, cancellationToken);
        var appUri = appSettings.AppUri.AbsoluteUri.TrimEnd('/');

        var member = new MemberVariables(notificationInfo.FirstName, notificationInfo.LastName);
        var variables = new BadgeAwardedEmailVariables(
            member,
            new BadgeVariables(
                oneTimeBadge.Name,
                oneTimeBadge.Description,
                hasImage ? $"{appUri}/api/Clubs/{oneTimeBadge.ClubId}/OneTimeBadges/{oneTimeBadgeId}/public-image" : null,
                IsOneTime: true),
            $"{appUri}/clubs/{oneTimeBadge.ClubId}/members/{memberId}/trophy-case");

        await mediator.Send(
            new SendClubEmail(oneTimeBadge.ClubId, ClubEmailType.BadgeAwarded, notificationInfo.Email, member.FullName, notificationInfo.Language, variables),
            cancellationToken);
    }
}
