using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Notifications;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

public record ClubAnnouncementRecipient(string Email, string FirstName, string LastName, Language Language);

/// <summary>Resolves who receives an announcement as email.</summary>
public static class ClubAnnouncementRecipients
{
    public enum ErrorCode
    {
        ClubAnnouncementRolesRequired = 0,
        ClubAnnouncementNoActiveSeason = 1,
        ClubAnnouncementNoRecipients = 2,
    }

    /// <summary>
    /// The club members of the audience that can be reached by email, one recipient per email address.
    /// Children without an own email are reached via a parent (same rule as "copy emails" in the members list).
    /// If several members share an address, the member the address belongs to is addressed.
    /// Addresses of users who switched off announcement emails in their notification preferences are left out.
    /// </summary>
    public static async Task<List<ClubAnnouncementRecipient>> Resolve(
        AppDbContext context,
        int clubId,
        ClubAnnouncementAudience audience,
        IReadOnlyCollection<MemberRole> roles,
        CancellationToken cancellationToken)
    {
        var filter = new MemberFilter { HasEmail = true };

        switch (audience)
        {
            case ClubAnnouncementAudience.AllMembers:
                break;
            case ClubAnnouncementAudience.ActiveSeasonMembers:
                var today = ClubAnnouncementMapper.Today;
                var activeSeasonId = await context.Seasons
                    .Where(s => s.ClubId == clubId && s.Period.From <= today && today <= s.Period.To)
                    .OrderByDescending(s => s.Period.From)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new PreconditionException(ErrorCode.ClubAnnouncementNoActiveSeason, "The club has no active season.");
                filter.InSeasonIds = [activeSeasonId];
                break;
            case ClubAnnouncementAudience.Youth:
                filter.AgeGroups = [AgeGroup.Kids, AgeGroup.Teenagers];
                break;
            case ClubAnnouncementAudience.Roles:
                if (roles.Count == 0)
                    throw new PreconditionException(ErrorCode.ClubAnnouncementRolesRequired, "At least one role is required.");
                filter.Roles = roles.Select(r => (Bookennis.Shared.Controller.Shared.MemberRole)(int)r).Distinct().ToArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown audience.");
        }

        var members = await MemberFilterQuery.Build(context, filter, cancellationToken);

        var rows = await members
            .Where(r => r.Member.ClubId == clubId)
            .OrderBy(r => r.User.LastName)
            .ThenBy(r => r.User.FirstName)
            .ThenBy(r => r.Member.Id)
            .Select(r => new { Email = r.ContactEmail!, r.User.FirstName, r.User.LastName, r.User.Language, IsOwnEmail = r.User.Email != null })
            .ToListAsync(cancellationToken);

        // No preference means the default, which is "on" (NotificationPreference.DefaultEmail)
        var optedOutEmails = (await (
            from preference in context.NotificationPreferences
            join user in context.Users on preference.UserId equals user.Id
            where preference.Type == NotificationType.ClubAnnouncement && !preference.Email && user.Email != null
            select user.Email
        ).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .Where(r => !optedOutEmails.Contains(r.Email))
            .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.IsOwnEmail).First())
            .Select(r => new ClubAnnouncementRecipient(r.Email, r.FirstName, r.LastName, r.Language))
            .ToList();
    }
}
