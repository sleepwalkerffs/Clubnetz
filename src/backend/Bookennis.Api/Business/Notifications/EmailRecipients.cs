using Bookennis.Api.Data;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.Notifications;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Notifications;

/// <summary>
/// A member that gets a notification email. <c>UserId</c> is the user the address belongs to (a parent for
/// children without an own email), the names and the language are the member's.
/// </summary>
public record EmailRecipient(int MemberId, int UserId, string Email, string FirstName, string LastName, Language Language);

/// <summary>
/// Resolves who gets a notification as email: one recipient per email address, without those who switched
/// emails off for the <see cref="NotificationType"/>. Children without an own email are reached via a parent
/// (same rule as <see cref="Members.MemberEmailResolver"/>), whose preferences then apply.
/// </summary>
public static class EmailRecipients
{
    public static async Task<List<EmailRecipient>> ForMembers(
        AppDbContext context,
        NotificationType type,
        IReadOnlyCollection<int> memberIds,
        IReadOnlyCollection<int>? exceptUserIds,
        CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
            return [];

        var members = context.Set<Member>().IgnoreQueryFilters().Where(m => memberIds.Contains(m.Id));
        return await Resolve(context, type, members, exceptUserIds, cancellationToken);
    }

    /// <summary>All club members (not guests) of a club.</summary>
    public static Task<List<EmailRecipient>> ForClub(
        AppDbContext context,
        NotificationType type,
        int clubId,
        IReadOnlyCollection<int>? exceptUserIds,
        CancellationToken cancellationToken)
    {
        var members = context.Set<Member>().IgnoreQueryFilters().Where(m => m.ClubId == clubId && m.MemberType == MemberType.ClubMember);
        return Resolve(context, type, members, exceptUserIds, cancellationToken);
    }

    private static async Task<List<EmailRecipient>> Resolve(
        AppDbContext context,
        NotificationType type,
        IQueryable<Member> members,
        IReadOnlyCollection<int>? exceptUserIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from member in members
            join user in context.Users on member.UserId equals user.Id
            let parent = (
                from childFm in context.FamilyMembers.OfType<ChildFamilyMember>()
                where childFm.MemberId == member.Id
                from parentFm in context.FamilyMembers.OfType<ParentFamilyMember>().Where(pf => pf.ParentFamilyId == childFm.ChildFamilyId)
                join parentMember in context.ClubMembers on parentFm.MemberId equals parentMember.Id
                join parentUser in context.Users on parentMember.UserId equals parentUser.Id
                where parentUser.Email != null
                orderby parentUser.LastName, parentUser.FirstName
                select new { UserId = (int?)parentUser.Id, parentUser.Email }
            ).FirstOrDefault()
            orderby member.Id
            select new
            {
                MemberId = member.Id,
                UserId = user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Language,
                ParentUserId = parent.UserId,
                ParentEmail = parent.Email
            }
        ).ToListAsync(cancellationToken);

        var candidates = rows
            .Select(r => r.Email is not null
                ? new { Recipient = new EmailRecipient(r.MemberId, r.UserId, r.Email, r.FirstName, r.LastName, r.Language), IsOwnEmail = true }
                : r.ParentEmail is not null && r.ParentUserId is not null
                    ? new { Recipient = new EmailRecipient(r.MemberId, r.ParentUserId.Value, r.ParentEmail, r.FirstName, r.LastName, r.Language), IsOwnEmail = false }
                    : null)
            .Where(c => c is not null && exceptUserIds?.Contains(c.Recipient.UserId) != true)
            .Select(c => c!)
            .ToList();

        if (candidates.Count == 0)
            return [];

        // No preference means the default, which is "on" (NotificationPreference.DefaultEmail)
        var userIds = candidates.Select(c => c.Recipient.UserId).Distinct().ToList();
        var optedOut = await context.NotificationPreferences
            .Where(p => p.Type == type && !p.Email && userIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        // If several members share an address (a parent and their children), the member the address belongs to is addressed
        return candidates
            .Where(c => !optedOut.Contains(c.Recipient.UserId))
            .GroupBy(c => c.Recipient.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.IsOwnEmail).First().Recipient)
            .ToList();
    }
}
