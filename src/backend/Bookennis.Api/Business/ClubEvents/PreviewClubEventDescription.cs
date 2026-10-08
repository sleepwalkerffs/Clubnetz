using Bookennis.Api.Business.ClubEmails;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Renders the (not yet saved) description of an event the way members will see it.</summary>
public record PreviewClubEventDescription(string Description) : IQuery<PreviewClubEventDescriptionResult>
{
    public class Handler(IClubEmailRenderer renderer) : IRequestHandler<PreviewClubEventDescription, PreviewClubEventDescriptionResult>
    {
        public Task<PreviewClubEventDescriptionResult> Handle(PreviewClubEventDescription request, CancellationToken cancellationToken)
        {
            if (request.Description.Length > ClubEvent.MaxDescriptionLength)
                throw new PreconditionException(ClubEvent.ErrorCode.ClubEventTextTooLong, "A text is too long.");

            return Task.FromResult(new PreviewClubEventDescriptionResult { Html = renderer.RenderMarkdown(request.Description) });
        }
    }
}
