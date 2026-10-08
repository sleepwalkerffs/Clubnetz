using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.OneTimeBadges;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetSeasonClubMembers(int ClubId, int SeasonId) : IQuery<GetSeasonClubMembersResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetSeasonClubMembers, GetSeasonClubMembersResult>
    {
        public async Task<GetSeasonClubMembersResult> Handle(GetSeasonClubMembers request, CancellationToken cancellationToken)
        {
            var members = await (from user in context.Users
                                 join member in context.ClubMembers on user.Id equals member.UserId
                                 where context.MemberSeasons.Any(ms => ms.MemberId == member.Id && ms.SeasonId == request.SeasonId)
                                 select new PlayerResult(member.Id, user.FirstName, user.LastName, false))
                                 .ToListAsync(cancellationToken);

            return new GetSeasonClubMembersResult(members);
        }
    }
}
