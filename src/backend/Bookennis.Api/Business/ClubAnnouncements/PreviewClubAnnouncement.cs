using Bookennis.Api.Business.ClubEmails;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.ClubAnnouncements;

namespace Bookennis.Api.Business.ClubAnnouncements;

/// <summary>Renders the (not yet saved) text of an announcement the way members will see it.</summary>
public record PreviewClubAnnouncement(string Body) : IQuery<PreviewClubAnnouncementResult>
{
    public class Handler(IClubEmailRenderer renderer) : IRequestHandler<PreviewClubAnnouncement, PreviewClubAnnouncementResult>
    {
        public Task<PreviewClubAnnouncementResult> Handle(PreviewClubAnnouncement request, CancellationToken cancellationToken)
        {
            if (request.Body.Length > ClubAnnouncement.MaxBodyLength)
                throw new PreconditionException(ClubAnnouncement.ErrorCode.ClubAnnouncementTextTooLong, "A text is too long.");

            return Task.FromResult(new PreviewClubAnnouncementResult { Html = renderer.RenderMarkdown(request.Body) });
        }
    }
}
