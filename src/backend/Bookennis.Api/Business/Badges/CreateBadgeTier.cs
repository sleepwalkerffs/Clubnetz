using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record CreateBadgeTier(int ClubId, int SeasonId, string Name, string Description, int MatchesRequired) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateBadgeTier, int>
    {
        public async Task<int> Handle(CreateBadgeTier request, CancellationToken cancellationToken)
        {
            var nextLevel = (await context.BadgeTiers
                .Where(bt => bt.ClubId == request.ClubId && bt.SeasonId == request.SeasonId)
                .Select(bt => (int?)bt.Level)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            var tier = new BadgeTier(request.ClubId, request.SeasonId, nextLevel, request.Name, request.Description, request.MatchesRequired, nextLevel);
            context.BadgeTiers.Add(tier);

            await context.SaveChangesAsync(cancellationToken);

            return tier.Id;
        }
    }
}
