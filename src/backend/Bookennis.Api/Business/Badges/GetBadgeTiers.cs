using Bookennis.Api.Data;
using Bookennis.Shared.Controller.BadgeTiers;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record GetBadgeTiers(int ClubId, int SeasonId) : IQuery<GetBadgeTiersResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBadgeTiers, GetBadgeTiersResult>
    {
        public async Task<GetBadgeTiersResult> Handle(GetBadgeTiers request, CancellationToken cancellationToken)
        {
            var tiers = await context.BadgeTiers
                .Where(bt => bt.ClubId == request.ClubId && bt.SeasonId == request.SeasonId)
                .OrderBy(bt => bt.Level)
                .Select(bt => new BadgeTierDto
                {
                    Id = bt.Id,
                    Level = bt.Level,
                    Name = bt.Name,
                    Description = bt.Description,
                    MatchesRequired = bt.MatchesRequired,
                    SortOrder = bt.SortOrder,
                    ImageUrl = context.BadgeTierImages.Any(i => i.BadgeTierId == bt.Id)
                        ? $"/api/Clubs/{request.ClubId}/BadgeTiers/{bt.Id}/image"
                        : null,
                    EarnedCount = context.MemberBadges.Count(mb => mb.BadgeTierId == bt.Id),
                })
                .ToListAsync(cancellationToken);

            return new GetBadgeTiersResult { Tiers = tiers };
        }
    }
}
