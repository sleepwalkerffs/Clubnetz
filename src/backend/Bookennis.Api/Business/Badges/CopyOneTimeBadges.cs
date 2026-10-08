using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record CopyOneTimeBadges(int ClubId, int SourceSeasonId, int TargetSeasonId) : ICommand
{
    public enum ErrorCode
    {
        SourceSeasonHasNoOneTimeBadges = 0,
        TargetSeasonAlreadyHasOneTimeBadges = 1
    }

    public class Handler(AppDbContext context) : IRequestHandler<CopyOneTimeBadges>
    {
        public async Task<Unit> Handle(CopyOneTimeBadges request, CancellationToken cancellationToken)
        {
            var sourceBadges = await context.OneTimeBadges
                .Where(b => b.ClubId == request.ClubId && b.SeasonId == request.SourceSeasonId)
                .OrderBy(b => b.Name)
                .ToListAsync(cancellationToken);

            if (sourceBadges.Count == 0)
                throw new PreconditionException(ErrorCode.SourceSeasonHasNoOneTimeBadges, "The selected season has no one-time badges to copy.");

            if (await context.OneTimeBadges.AnyAsync(b => b.ClubId == request.ClubId && b.SeasonId == request.TargetSeasonId, cancellationToken))
                throw new PreconditionException(ErrorCode.TargetSeasonAlreadyHasOneTimeBadges, "The target season already has one-time badges configured.");

            var sourceImagesByBadgeId = await context.OneTimeBadgeImages
                .Where(i => sourceBadges.Select(b => b.Id).Contains(i.OneTimeBadgeId))
                .ToDictionaryAsync(i => i.OneTimeBadgeId, cancellationToken);

            // Copies only the category (name/description/image) - never the awardees, since
            // e.g. last season's champion must not carry over as this season's champion.
            var newBadges = sourceBadges
                .Select(badge => new
                {
                    SourceBadgeId = badge.Id,
                    NewBadge = new OneTimeBadge(request.ClubId, request.TargetSeasonId, badge.Name, badge.Description)
                })
                .ToList();

            context.OneTimeBadges.AddRange(newBadges.Select(b => b.NewBadge));
            await context.SaveChangesAsync(cancellationToken);

            foreach (var b in newBadges)
            {
                if (sourceImagesByBadgeId.TryGetValue(b.SourceBadgeId, out var sourceImage))
                    context.OneTimeBadgeImages.Add(new OneTimeBadgeImage(b.NewBadge.Id, sourceImage.Data, sourceImage.ContentType));
            }

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
