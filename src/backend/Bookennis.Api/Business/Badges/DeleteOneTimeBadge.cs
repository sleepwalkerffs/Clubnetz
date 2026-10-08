using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record DeleteOneTimeBadge(int ClubId, int OneTimeBadgeId) : ICommand
{
    public enum ErrorCode
    {
        OneTimeBadgeAlreadyAwarded = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<DeleteOneTimeBadge>
    {
        public async Task<Unit> Handle(DeleteOneTimeBadge request, CancellationToken cancellationToken)
        {
            var badge = await context.OneTimeBadges
                .SingleRequiredAsync(b => b.Id == request.OneTimeBadgeId && b.ClubId == request.ClubId, cancellationToken);

            if (await context.MemberOneTimeBadges.AnyAsync(motb => motb.OneTimeBadgeId == badge.Id, cancellationToken))
                throw new PreconditionException(ErrorCode.OneTimeBadgeAlreadyAwarded, "Cannot delete a one-time badge that has already been awarded to a member.");

            context.OneTimeBadges.Remove(badge);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
