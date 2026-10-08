using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubEvents;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEvents;

public record GetClubEvent(int ClubId, int UserId, int ClubEventId) : IQuery<ClubEventDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<GetClubEvent, ClubEventDto>
    {
        public async Task<ClubEventDto> Handle(GetClubEvent request, CancellationToken cancellationToken)
        {
            var clubEvent = await context.ClubEvents.AsNoTracking().GetEventWithDetails(request.ClubId, request.ClubEventId, cancellationToken);
            var myMemberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var members = await ClubEventMapper.LoadMemberInfos(context, clubEvent, cancellationToken);

            return ClubEventMapper.ToDto(renderer, clubEvent, members, myMemberId, DateTimeOffset.UtcNow);
        }
    }
}
