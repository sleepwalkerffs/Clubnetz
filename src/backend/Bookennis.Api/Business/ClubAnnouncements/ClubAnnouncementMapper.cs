using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubAnnouncements;
using Microsoft.EntityFrameworkCore;
using ClubAnnouncementAudience = Bookennis.Shared.Controller.ClubAnnouncements.ClubAnnouncementAudience;

namespace Bookennis.Api.Business.ClubAnnouncements;

public static class ClubAnnouncementMapper
{
    public const int ExcerptLength = 220;

    /// <summary>The day that decides whether an announcement is expired.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static ClubAnnouncement.AnnouncementData ToData(SaveClubAnnouncementModel model)
        => new(model.Title ?? "", model.Body ?? "", model.IsPinned, model.ExpiresOn);

    public static string Excerpt(IClubEmailRenderer renderer, string body)
    {
        var text = renderer.ToPlainText(body);
        return text.Length <= ExcerptLength ? text : text[..ExcerptLength].TrimEnd() + "…";
    }

    /// <summary><paramref name="canManage"/>: the email details are only returned to members who manage the announcements.</summary>
    public static async Task<ClubAnnouncementDto> ToDto(AppDbContext context, IClubEmailRenderer renderer, ClubAnnouncement announcement, bool canManage, CancellationToken cancellationToken)
    {
        var createdByName = announcement.CreatedByMemberId is { } memberId
            ? await (from member in context.Set<Member>()
                     join user in context.Users on member.UserId equals user.Id
                     where member.Id == memberId
                     select user.FirstName + " " + user.LastName).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ClubAnnouncementDto
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Body = announcement.Body,
            BodyHtml = renderer.RenderMarkdown(announcement.Body),
            IsPinned = announcement.IsPinned,
            PublishedAt = announcement.PublishedAt,
            ExpiresOn = announcement.ExpiresOn,
            IsExpired = announcement.IsExpired(Today),
            CreatedByName = createdByName,
            Attachments = announcement.Attachments
                .OrderBy(a => a.Id)
                .Select(a => new ClubAnnouncementAttachmentDto { Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Size = a.Size })
                .ToList(),
            EmailSentAt = canManage ? announcement.EmailSentAt : null,
            EmailAudience = canManage ? (ClubAnnouncementAudience?)announcement.EmailAudience : null,
            EmailRecipientCount = canManage ? announcement.EmailRecipientCount : null,
        };
    }
}
