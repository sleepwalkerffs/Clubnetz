using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Push;

/// <summary>
/// Resolves who gets a push notification. Only users with at least one registered device are returned, and
/// not those who switched push notifications off for the <see cref="NotificationType"/>.
/// </summary>
public static class PushRecipients
{
    /// <summary>
    /// The users behind the members. A member without own login (a child) is represented by the user it belongs to.
    /// </summary>
    public static async Task<List<PushRecipient>> ForMembers(AppDbContext context, NotificationType type, IReadOnlyCollection<int> memberIds, int? exceptUserId, CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
            return [];

        var userIds = await (
            from member in context.Set<Member>().IgnoreQueryFilters()
            join user in context.Users on member.UserId equals user.Id
            where memberIds.Contains(member.Id)
            select user.BelongsToUserId ?? user.Id
        ).Distinct().ToListAsync(cancellationToken);

        return await ForUsers(context, type, userIds, exceptUserId, cancellationToken);
    }

    /// <summary>All club members (not guests) of a club.</summary>
    public static async Task<List<PushRecipient>> ForClub(AppDbContext context, NotificationType type, int clubId, int? exceptUserId, CancellationToken cancellationToken)
    {
        var userIds = await context.ClubMembers
            .IgnoreQueryFilters()
            .Where(m => m.ClubId == clubId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await ForUsers(context, type, userIds, exceptUserId, cancellationToken);
    }

    /// <summary>Pass no <paramref name="type"/> for notifications the user can't switch off (e.g. the test notification).</summary>
    public static Task<List<PushRecipient>> ForUsers(AppDbContext context, NotificationType? type, IReadOnlyCollection<int> userIds, int? exceptUserId, CancellationToken cancellationToken)
        => context.Users
            .Where(u => userIds.Contains(u.Id) && u.Id != exceptUserId && context.PushSubscriptions.Any(s => s.UserId == u.Id))
            // No preference means the default, which is "on" (NotificationPreference.DefaultPush)
            .Where(u => type == null || !context.NotificationPreferences.Any(p => p.UserId == u.Id && p.Type == type && !p.Push))
            .Select(u => new PushRecipient(u.Id, u.Language))
            .ToListAsync(cancellationToken);
}
