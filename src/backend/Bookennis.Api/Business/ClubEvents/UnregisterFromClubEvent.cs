using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Withdraws the current member's registration.</summary>
public record UnregisterFromClubEvent(int ClubId, int UserId, int ClubEventId) : ICommand<ClubEventDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<UnregisterFromClubEvent, ClubEventDto>
    {
        public async Task<ClubEventDto> Handle(UnregisterFromClubEvent request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var clubEvent = await context.ClubEvents.GetEventWithDetails(request.ClubId, request.ClubEventId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            clubEvent.Unregister(memberId, now);

            await context.SaveChangesAsync(cancellationToken);

            var members = await ClubEventMapper.LoadMemberInfos(context, clubEvent, cancellationToken);
            return ClubEventMapper.ToDto(renderer, clubEvent, members, memberId, now);
        }
    }
}
