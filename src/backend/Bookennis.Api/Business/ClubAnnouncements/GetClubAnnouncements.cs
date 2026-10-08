using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubAnnouncements;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>
/// The announcements of a club: pinned first, then the newest. Expired announcements are only returned
/// (at the end) when <paramref name="IncludeExpired"/> is set by a member who manages the announcements.
/// </summary>
public record GetClubAnnouncements(int ClubId, bool CanManage, bool IncludeExpired = false, int? Take = null) : IQuery<GetClubAnnouncementsResult>
{
    public const int MaxCount = 100;

    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<GetClubAnnouncements, GetClubAnnouncementsResult>
    {
        public async Task<GetClubAnnouncementsResult> Handle(GetClubAnnouncements request, CancellationToken cancellationToken)
        {
            var today = ClubAnnouncementMapper.Today;
            var take = Math.Clamp(request.Take ?? MaxCount, 1, MaxCount);

            var query = context.ClubAnnouncements.Where(a => a.ClubId == request.ClubId);
            if (!request.CanManage || !request.IncludeExpired)
                query = query.Where(a => a.ExpiresOn == null || a.ExpiresOn >= today);

            var announcements = await query
                .OrderBy(a => a.ExpiresOn != null && a.ExpiresOn < today)
                .ThenByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.Id)
                .Take(take)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Body,
                    a.IsPinned,
                    a.PublishedAt,
                    a.ExpiresOn,
                    AttachmentCount = a.Attachments.Count,
                    CreatedByName = (from member in context.Set<Member>()
                                     join user in context.Users on member.UserId equals user.Id
                                     where member.Id == a.CreatedByMemberId
                                     select user.FirstName + " " + user.LastName).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            return new GetClubAnnouncementsResult
            {
                Announcements = announcements
                    .Select(a => new ClubAnnouncementSummaryDto
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Excerpt = ClubAnnouncementMapper.Excerpt(renderer, a.Body),
                        IsPinned = a.IsPinned,
                        PublishedAt = a.PublishedAt,
                        ExpiresOn = a.ExpiresOn,
                        IsExpired = a.ExpiresOn < today,
                        AttachmentCount = a.AttachmentCount,
                        CreatedByName = a.CreatedByName,
                    })
                    .ToList()
            };
        }
    }
}
