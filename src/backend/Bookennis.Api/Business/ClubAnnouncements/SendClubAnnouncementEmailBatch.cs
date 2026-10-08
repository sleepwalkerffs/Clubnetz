using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Fusonic.Extensions.Email;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>
/// Sends an announcement to one batch of recipients with the club email <see cref="ClubEmailType.Announcement"/>.
/// Use <see cref="SendClubAnnouncementEmail"/>, it resolves the recipients and creates the batches.
/// </summary>
public record SendClubAnnouncementEmailBatch(int ClubId, int ClubAnnouncementId, ClubAnnouncementRecipient[] Recipients) : ICommand
{
    // Runs as a background job: rendering and queueing the emails of a large club must not block the request
    [OutOfBand]
    public class Handler(AppDbContext context, IMediator mediator, AppSettings appSettings) : IRequestHandler<SendClubAnnouncementEmailBatch>
    {
        public async Task<Unit> Handle(SendClubAnnouncementEmailBatch request, CancellationToken cancellationToken)
        {
            // Background jobs have no tenant, the club is part of the request
            var announcement = await context.ClubAnnouncements
                .IgnoreQueryFilters()
                .Where(a => a.Id == request.ClubAnnouncementId && a.ClubId == request.ClubId)
                .Select(a => new
                {
                    a.Title,
                    a.Body,
                    Attachments = a.Attachments.OrderBy(x => x.Id).Select(x => new { x.Id, x.FileName }).ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);

            // Deleted in the meantime
            if (announcement is null)
                return default;

            var appUri = appSettings.AppUri.AbsoluteUri.TrimEnd('/');
            var variables = new AnnouncementVariables(
                announcement.Title,
                new MarkdownText(announcement.Body),
                $"{appUri}/clubs/{request.ClubId}/news/{request.ClubAnnouncementId}");

            var attachments = announcement.Attachments
                .Select(a => new Attachment(a.FileName, ClubAnnouncementAttachmentResolver.CreateUri(a.Id)))
                .ToList();

            foreach (var recipient in request.Recipients)
            {
                var member = new MemberVariables(recipient.FirstName, recipient.LastName);
                await mediator.Send(
                    new SendClubEmail(
                        request.ClubId,
                        ClubEmailType.Announcement,
                        recipient.Email,
                        member.FullName,
                        recipient.Language,
                        new AnnouncementEmailVariables(member, variables),
                        Attachments: attachments),
                    cancellationToken);
            }

            return default;
        }
    }
}
