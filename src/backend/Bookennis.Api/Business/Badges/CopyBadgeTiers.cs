using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record CopyBadgeTiers(int ClubId, int SourceSeasonId, int TargetSeasonId) : ICommand
{
    public enum ErrorCode
    {
        SourceSeasonHasNoBadgeTiers = 0,
        TargetSeasonAlreadyHasBadgeTiers = 1
    }

    public class Handler(AppDbContext context) : IRequestHandler<CopyBadgeTiers>
    {
        public async Task<Unit> Handle(CopyBadgeTiers request, CancellationToken cancellationToken)
        {
            var sourceTiers = await context.BadgeTiers
                .Where(bt => bt.ClubId == request.ClubId && bt.SeasonId == request.SourceSeasonId)
                .OrderBy(bt => bt.Level)
                .ToListAsync(cancellationToken);

            if (sourceTiers.Count == 0)
                throw new PreconditionException(ErrorCode.SourceSeasonHasNoBadgeTiers, "The selected season has no badge tiers to copy.");

            if (await context.BadgeTiers.AnyAsync(bt => bt.ClubId == request.ClubId && bt.SeasonId == request.TargetSeasonId, cancellationToken))
                throw new PreconditionException(ErrorCode.TargetSeasonAlreadyHasBadgeTiers, "The target season already has badge tiers configured.");

            var sourceImagesByTierId = await context.BadgeTierImages
                .Where(i => sourceTiers.Select(t => t.Id).Contains(i.BadgeTierId))
                .ToDictionaryAsync(i => i.BadgeTierId, cancellationToken);

            var newTiers = sourceTiers
                .Select(tier => new
                {
                    SourceTierId = tier.Id,
                    NewTier = new BadgeTier(request.ClubId, request.TargetSeasonId, tier.Level, tier.Name, tier.Description, tier.MatchesRequired, tier.SortOrder)
                })
                .ToList();

            context.BadgeTiers.AddRange(newTiers.Select(t => t.NewTier));
            await context.SaveChangesAsync(cancellationToken);

            foreach (var t in newTiers)
            {
                if (sourceImagesByTierId.TryGetValue(t.SourceTierId, out var sourceImage))
                    context.BadgeTierImages.Add(new BadgeTierImage(t.NewTier.Id, sourceImage.Data, sourceImage.ContentType));
            }

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
