using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubAnnouncements;
using ClubAnnouncementAudience = Bookennis.Domain.ClubAnnouncements.ClubAnnouncementAudience;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>
/// Sends an announcement including its attachments to an audience as email. The recipients are resolved
/// right away and handed to background jobs in batches (<see cref="SendClubAnnouncementEmailBatch"/>),
/// so the request returns immediately and a failing batch does not affect the others.
/// </summary>
public record SendClubAnnouncementEmail(int ClubId, int ClubAnnouncementId, ClubAnnouncementAudience Audience, IReadOnlyCollection<MemberRole> Roles) : ICommand<ClubAnnouncementDto>
{
    public const int BatchSize = 50;

    public class Handler(AppDbContext context, IMediator mediator, IClubEmailRenderer renderer) : IRequestHandler<SendClubAnnouncementEmail, ClubAnnouncementDto>
    {
        public async Task<ClubAnnouncementDto> Handle(SendClubAnnouncementEmail request, CancellationToken cancellationToken)
        {
            var announcement = await context.ClubAnnouncements.GetAnnouncementWithAttachments(request.ClubId, request.ClubAnnouncementId, cancellationToken);

            var recipients = await ClubAnnouncementRecipients.Resolve(context, request.ClubId, request.Audience, request.Roles, cancellationToken);
            if (recipients.Count == 0)
                throw new PreconditionException(ClubAnnouncementRecipients.ErrorCode.ClubAnnouncementNoRecipients, "Nobody in this audience can be reached by email.");

            announcement.MarkEmailSent(request.Audience, recipients.Count, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var batch in recipients.Chunk(BatchSize))
                await mediator.Send(new SendClubAnnouncementEmailBatch(request.ClubId, request.ClubAnnouncementId, batch), cancellationToken);

            return await ClubAnnouncementMapper.ToDto(context, renderer, announcement, canManage: true, cancellationToken);
        }
    }
}
