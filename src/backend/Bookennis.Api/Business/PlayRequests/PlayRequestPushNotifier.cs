using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Domain.Notifications;
using Bookennis.Domain.PlayRequests;
using Bookennis.Global;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.PlayRequests;

/// <summary>
/// Tells the members of a play request what happens to it. <b>Push only</b>, there is no email for play requests.
/// Open requests are also listed in the app, so a member without push still sees them.
/// </summary>
public interface IPlayRequestPushNotifier
{
    /// <summary>The members who were asked.</summary>
    Task RequestCreated(int playRequestId, CancellationToken cancellationToken);

    /// <summary>The requester hears that somebody accepted and how many players are still missing (not when it is full: the booking tells that).</summary>
    Task Accepted(int playRequestId, int acceptedMemberId, CancellationToken cancellationToken);

    /// <summary>The members who were asked and have not declined.</summary>
    Task Cancelled(int playRequestId, CancellationToken cancellationToken);
}

public class PlayRequestPushNotifier(AppDbContext context, IPushNotificationService pushNotificationService) : IPlayRequestPushNotifier
{
    public async Task RequestCreated(int playRequestId, CancellationToken cancellationToken)
    {
        var info = await Load(playRequestId, cancellationToken);
        if (info is null)
            return;

        var recipients = await PushRecipients.ForMembers(context, NotificationType.PlayRequest, info.InviteeIds, info.RequesterUserId, cancellationToken);
        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "PlayRequest_Title", info.RequesterName),
                PushTexts.Get(culture, "PlayRequest_Body", info.PlayModeName, PushTexts.DateAndTime(culture, info.LocalStart)),
                Url(info),
                Tag: Tag(playRequestId)),
            cancellationToken);
    }

    public async Task Accepted(int playRequestId, int acceptedMemberId, CancellationToken cancellationToken)
    {
        var info = await Load(playRequestId, cancellationToken);
        if (info is null)
            return;

        var acceptor = await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where member.Id == acceptedMemberId
            select new { user.Id, user.FullName }
        ).SingleOrDefaultAsync(cancellationToken);

        if (acceptor is null)
            return;

        var recipients = await PushRecipients.ForMembers(context, NotificationType.PlayRequest, [info.RequesterId], acceptor.Id, cancellationToken);
        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "PlayRequestAccepted_Title", acceptor.FullName),
                PushTexts.Get(culture, "PlayRequestAccepted_Body", PushTexts.DateAndTime(culture, info.LocalStart), info.OpenSpots),
                Url(info),
                Tag: Tag(playRequestId)),
            cancellationToken);
    }

    public async Task Cancelled(int playRequestId, CancellationToken cancellationToken)
    {
        var info = await Load(playRequestId, cancellationToken);
        if (info is null)
            return;

        var recipients = await PushRecipients.ForMembers(context, NotificationType.PlayRequest, info.AskedIds, info.RequesterUserId, cancellationToken);
        await pushNotificationService.Notify(
            recipients,
            culture => new PushNotification(
                PushTexts.Get(culture, "PlayRequestCancelled_Title", info.RequesterName),
                PushTexts.Get(culture, "PlayRequestCancelled_Body", PushTexts.DateAndTime(culture, info.LocalStart)),
                Url(info),
                Tag: Tag(playRequestId)),
            cancellationToken);
    }

    private static string Url(Info info) => $"/clubs/{info.ClubId}/play-requests";

    private static string Tag(int playRequestId) => $"play-request-{playRequestId}";

    private async Task<Info?> Load(int playRequestId, CancellationToken cancellationToken)
    {
        var request = await (
            from r in context.PlayRequests.IgnoreQueryFilters()
            join member in context.ClubMembers.IgnoreQueryFilters() on r.RequesterMemberId equals member.Id
            join user in context.Users on member.UserId equals user.Id
            join playMode in context.PlayModes.IgnoreQueryFilters() on r.PlayModeId equals playMode.Id
            where r.Id == playRequestId
            select new
            {
                r.ClubId,
                r.StartsAt,
                r.TimeZoneInfoId,
                r.PlayersNeeded,
                RequesterId = r.RequesterMemberId,
                RequesterUserId = user.Id,
                RequesterName = user.FullName,
                PlayModeName = playMode.Name,
                Invitees = r.Invitees.Select(i => new { i.MemberId, i.Status }).ToList(),
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (request is null)
            return null;

        var accepted = request.Invitees.Count(i => i.Status == PlayRequestInviteeStatus.Accepted);
        return new Info(
            request.ClubId,
            request.StartsAt.AddTimeZoneInfo(request.TimeZoneInfoId),
            request.RequesterId,
            request.RequesterUserId,
            request.RequesterName,
            request.PlayModeName,
            request.Invitees.Where(i => i.Status == PlayRequestInviteeStatus.Invited).Select(i => i.MemberId).ToList(),
            request.Invitees.Where(i => i.Status != PlayRequestInviteeStatus.Declined).Select(i => i.MemberId).ToList(),
            request.PlayersNeeded - 1 - accepted);
    }

    private sealed record Info(
        int ClubId,
        DateTimeOffset LocalStart,
        int RequesterId,
        int RequesterUserId,
        string RequesterName,
        string PlayModeName,
        List<int> InviteeIds,
        List<int> AskedIds,
        int OpenSpots);
}
