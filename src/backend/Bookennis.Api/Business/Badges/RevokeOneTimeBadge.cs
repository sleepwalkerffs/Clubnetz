using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record RevokeOneTimeBadge(int ClubId, int OneTimeBadgeId, int MemberId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<RevokeOneTimeBadge>
    {
        public async Task<Unit> Handle(RevokeOneTimeBadge request, CancellationToken cancellationToken)
        {
            var award = await context.MemberOneTimeBadges
                .FirstOrDefaultAsync(motb => motb.OneTimeBadgeId == request.OneTimeBadgeId && motb.MemberId == request.MemberId, cancellationToken);

            if (award is null)
                return default;

            context.MemberOneTimeBadges.Remove(award);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
