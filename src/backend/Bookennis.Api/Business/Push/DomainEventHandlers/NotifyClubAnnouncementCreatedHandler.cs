using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Base;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Notifications;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push.DomainEventHandlers;

/// <summary>Push notification for the club members when the club publishes an announcement.</summary>
public class NotifyClubAnnouncementCreatedHandler(AppDbContext context, IPushNotificationService pushNotificationService, IUserAccessor userAccessor)
    : INotificationHandler<DomainEvent<EntityCreated<ClubAnnouncement>>>
{
    public async Task Handle(DomainEvent<EntityCreated<ClubAnnouncement>> notification, CancellationToken cancellationToken)
    {
        var announcement = await (
            from a in context.ClubAnnouncements.IgnoreQueryFilters()
            join club in context.Clubs.IgnoreQueryFilters() on a.ClubId equals club.Id
            where a.Id == notification.EntityId
            select new { a.Id, a.ClubId, a.Title, a.ExpiresOn, ClubName = club.Name }
        ).SingleOrDefaultAsync(cancellationToken);

        if (announcement is null || announcement.ExpiresOn < ClubAnnouncementMapper.Today)
            return;

        var authorUserId = userAccessor.TryGetUserId(out var userId) ? userId : (int?)null;
        var recipients = await PushRecipients.ForClub(context, NotificationType.ClubAnnouncement, announcement.ClubId, authorUserId, cancellationToken);

        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "ClubAnnouncementCreated_Title", announcement.ClubName),
                announcement.Title,
                $"/clubs/{announcement.ClubId}/news/{announcement.Id}",
                Tag: $"club-announcement-{announcement.Id}"),
            cancellationToken);
    }
}
