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

public class SendMemberBadgeAwardedEmailHandler(
    AppDbContext context,
    IMediator mediator,
    AppSettings appSettings) : INotificationHandler<DomainEvent<MemberBadgeAwardedDomainEvent>>
{
    public async Task Handle(DomainEvent<MemberBadgeAwardedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var memberId = notification.Event.MemberId;
        var badgeTierId = notification.Event.BadgeTierId;

        // The member's own email or a parent's, unless they don't want badge emails
        var notificationInfo = (await EmailRecipients.ForMembers(context, NotificationType.BadgeAwarded, [memberId], exceptUserIds: null, cancellationToken)).FirstOrDefault();
        if (notificationInfo is null)
            return;

        var badgeTier = await context.BadgeTiers
            .Where(bt => bt.Id == badgeTierId)
            .Select(bt => new { bt.Name, bt.Description, bt.ClubId })
            .SingleOrDefaultAsync(cancellationToken);

        if (badgeTier is null)
            return;

        var hasImage = await context.BadgeTierImages.AnyAsync(i => i.BadgeTierId == badgeTierId, cancellationToken);
        var appUri = appSettings.AppUri.AbsoluteUri.TrimEnd('/');

        var member = new MemberVariables(notificationInfo.FirstName, notificationInfo.LastName);
        var variables = new BadgeAwardedEmailVariables(
            member,
            new BadgeVariables(
                badgeTier.Name,
                badgeTier.Description,
                hasImage ? $"{appUri}/api/Clubs/{badgeTier.ClubId}/BadgeTiers/{badgeTierId}/public-image" : null,
                IsOneTime: false),
            $"{appUri}/clubs/{badgeTier.ClubId}/members/{memberId}/trophy-case");

        await mediator.Send(
            new SendClubEmail(badgeTier.ClubId, ClubEmailType.BadgeAwarded, notificationInfo.Email, member.FullName, notificationInfo.Language, variables),
            cancellationToken);
    }
}
