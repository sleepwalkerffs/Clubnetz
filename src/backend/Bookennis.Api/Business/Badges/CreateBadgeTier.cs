using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

/// <summary>Adds a tier to a season. <c>Sport</c> is the track of the tier: only matches on courts of this sport count; <c>null</c> = every sport. Levels are numbered per track.</summary>
public record CreateBadgeTier(int ClubId, int SeasonId, string Name, string Description, int MatchesRequired, Sport? Sport = null) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateBadgeTier, int>
    {
        public async Task<int> Handle(CreateBadgeTier request, CancellationToken cancellationToken)
        {
            // Levels are numbered per track
            var nextLevel = (await context.BadgeTiers
                .Where(bt => bt.ClubId == request.ClubId && bt.SeasonId == request.SeasonId && bt.Sport == request.Sport)
                .Select(bt => (int?)bt.Level)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            var tier = new BadgeTier(request.ClubId, request.SeasonId, nextLevel, request.Name, request.Description, request.MatchesRequired, nextLevel, request.Sport);
            context.BadgeTiers.Add(tier);

            await context.SaveChangesAsync(cancellationToken);

            return tier.Id;
        }
    }
}
