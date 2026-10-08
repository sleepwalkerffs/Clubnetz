using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubEvents;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Replaces all event data including the questions. Answers to removed options are deleted, registrations are kept.</summary>
public record UpdateClubEvent(int ClubId, int UserId, int ClubEventId, ClubEvent.EventData Data) : ICommand<ClubEventDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<UpdateClubEvent, ClubEventDto>
    {
        public async Task<ClubEventDto> Handle(UpdateClubEvent request, CancellationToken cancellationToken)
        {
            var clubEvent = await context.ClubEvents.GetEventWithDetails(request.ClubId, request.ClubEventId, cancellationToken);

            clubEvent.Update(request.Data);

            await context.SaveChangesAsync(cancellationToken);

            var myMemberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var members = await ClubEventMapper.LoadMemberInfos(context, clubEvent, cancellationToken);
            return ClubEventMapper.ToDto(renderer, clubEvent, members, myMemberId, DateTimeOffset.UtcNow);
        }
    }
}
