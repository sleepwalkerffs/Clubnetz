using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Bookennis.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Notifications;

/// <summary>
/// Tells club members about a club event by push notification and email, each according to the members'
/// notification preferences.
/// </summary>
public interface IClubEventNotifier
{
    /// <summary>A new event was published: all club members except the creator.</summary>
    Task EventCreated(int clubEventId, int? creatorUserId, CancellationToken cancellationToken);

    /// <summary>The event is coming up: the registered members.</summary>
    Task EventReminder(int clubEventId, CancellationToken cancellationToken);

    /// <summary>The registration closes soon: the club members who have not registered.</summary>
    Task RegistrationDeadlineReminder(int clubEventId, CancellationToken cancellationToken);
}

public class ClubEventNotifier(AppDbContext context, IPushNotificationService pushNotificationService, IMediator mediator) : IClubEventNotifier
{
    public const int EmailBatchSize = 50;

    public async Task EventCreated(int clubEventId, int? creatorUserId, CancellationToken cancellationToken)
    {
        var clubEvent = await GetEvent(clubEventId, cancellationToken);
        if (clubEvent is null)
            return;

        await Notify(
            clubEvent,
            "ClubEventCreated_Title",
            ClubEmailType.ClubEventCreated,
            await PushRecipients.ForClub(context, NotificationType.ClubEventCreated, clubEvent.ClubId, creatorUserId, cancellationToken),
            await EmailRecipients.ForClub(context, NotificationType.ClubEventCreated, clubEvent.ClubId, creatorUserId is null ? null : [creatorUserId.Value], cancellationToken),
            cancellationToken);
    }

    public async Task EventReminder(int clubEventId, CancellationToken cancellationToken)
    {
        var clubEvent = await GetEvent(clubEventId, cancellationToken);
        if (clubEvent is null)
            return;

        var memberIds = await context.ClubEventRegistrations
            .Where(r => r.ClubEventId == clubEventId)
            .Select(r => r.MemberId)
            .ToListAsync(cancellationToken);

        await Notify(
            clubEvent,
            "ClubEventReminder_Title",
            ClubEmailType.ClubEventReminder,
            await PushRecipients.ForMembers(context, NotificationType.ClubEventReminder, memberIds, exceptUserId: null, cancellationToken),
            await EmailRecipients.ForMembers(context, NotificationType.ClubEventReminder, memberIds, exceptUserIds: null, cancellationToken),
            cancellationToken);
    }

    public async Task RegistrationDeadlineReminder(int clubEventId, CancellationToken cancellationToken)
    {
        var clubEvent = await GetEvent(clubEventId, cancellationToken);
        if (clubEvent?.RegistrationDeadline is null)
            return;

        // Whoever registered is done, also for the children they receive notifications for
        var registeredUserIds = await (
            from registration in context.ClubEventRegistrations
            join member in context.Set<Member>().IgnoreQueryFilters() on registration.MemberId equals member.Id
            where registration.ClubEventId == clubEventId
            select member.UserId
        ).ToListAsync(cancellationToken);

        var openUserIds = await context.Set<Member>()
            .IgnoreQueryFilters()
            .Where(m => m.ClubId == clubEvent.ClubId && m.MemberType == MemberType.ClubMember && !registeredUserIds.Contains(m.UserId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await Notify(
            clubEvent,
            "ClubEventDeadline_Title",
            ClubEmailType.ClubEventRegistrationDeadline,
            await PushRecipients.ForUsers(context, NotificationType.ClubEventRegistrationDeadline, openUserIds, exceptUserId: null, cancellationToken),
            await EmailRecipients.ForClub(context, NotificationType.ClubEventRegistrationDeadline, clubEvent.ClubId, registeredUserIds, cancellationToken),
            cancellationToken);
    }

    private async Task Notify(
        ClubEventInfo clubEvent,
        string pushTitleKey,
        ClubEmailType emailType,
        List<PushRecipient> pushRecipients,
        List<EmailRecipient> emailRecipients,
        CancellationToken cancellationToken)
    {
        await pushNotificationService.Notify(
            pushRecipients,
            culture =>
            {
                string?[] details = emailType == ClubEmailType.ClubEventRegistrationDeadline
                    ? [PushTexts.Get(culture, "ClubEventDeadline_Body", PushTexts.DateAndTime(culture, clubEvent.LocalRegistrationDeadline!.Value)), clubEvent.ClubName]
                    : [clubEvent.PushDate(culture), clubEvent.Location, clubEvent.ClubName];

                return new PushNotification(
                    PushTexts.Get(culture, pushTitleKey, clubEvent.Title),
                    string.Join(" · ", details.Where(d => !string.IsNullOrWhiteSpace(d))),
                    $"/clubs/{clubEvent.ClubId}/calendar/{clubEvent.Id}",
                    Tag: $"club-event-{clubEvent.Id}");
            },
            cancellationToken);

        foreach (var batch in emailRecipients.Chunk(EmailBatchSize))
            await mediator.Send(new SendClubEventEmailBatch(clubEvent.ClubId, clubEvent.Id, emailType, batch), cancellationToken);
    }

    private Task<ClubEventInfo?> GetEvent(int clubEventId, CancellationToken cancellationToken)
        => ClubEventInfo.Get(context, clubEventId, clubId: null, cancellationToken);
}
