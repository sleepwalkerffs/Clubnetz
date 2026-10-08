using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record AwardOneTimeBadge(int ClubId, int OneTimeBadgeId, List<int> MemberIds) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AwardOneTimeBadge>
    {
        public async Task<Unit> Handle(AwardOneTimeBadge request, CancellationToken cancellationToken)
        {
            await context.OneTimeBadges.SingleRequiredAsync(b => b.Id == request.OneTimeBadgeId && b.ClubId == request.ClubId, cancellationToken);

            var alreadyAwardedMemberIds = await context.MemberOneTimeBadges
                .Where(motb => motb.OneTimeBadgeId == request.OneTimeBadgeId && request.MemberIds.Contains(motb.MemberId))
                .Select(motb => motb.MemberId)
                .ToListAsync(cancellationToken);

            var newMemberIds = request.MemberIds.Except(alreadyAwardedMemberIds);

            foreach (var memberId in newMemberIds)
                context.MemberOneTimeBadges.Add(new MemberOneTimeBadge(memberId, request.OneTimeBadgeId));

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
