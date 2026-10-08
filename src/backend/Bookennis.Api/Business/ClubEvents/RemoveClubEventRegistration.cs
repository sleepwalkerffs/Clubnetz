using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Removes any member's registration (organizers only, also after the deadline).</summary>
public record RemoveClubEventRegistration(int ClubId, int UserId, int ClubEventId, int RegistrationId) : ICommand<ClubEventDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<RemoveClubEventRegistration, ClubEventDto>
    {
        public async Task<ClubEventDto> Handle(RemoveClubEventRegistration request, CancellationToken cancellationToken)
        {
            var clubEvent = await context.ClubEvents.GetEventWithDetails(request.ClubId, request.ClubEventId, cancellationToken);

            clubEvent.RemoveRegistration(request.RegistrationId);

            await context.SaveChangesAsync(cancellationToken);

            var myMemberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var members = await ClubEventMapper.LoadMemberInfos(context, clubEvent, cancellationToken);
            return ClubEventMapper.ToDto(renderer, clubEvent, members, myMemberId, DateTimeOffset.UtcNow);
        }
    }
}
