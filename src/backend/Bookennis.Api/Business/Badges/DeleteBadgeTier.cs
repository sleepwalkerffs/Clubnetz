using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record DeleteBadgeTier(int ClubId, int TierId) : ICommand
{
    public enum ErrorCode
    {
        BadgeTierAlreadyEarned = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<DeleteBadgeTier>
    {
        public async Task<Unit> Handle(DeleteBadgeTier request, CancellationToken cancellationToken)
        {
            var tier = await context.BadgeTiers
                .SingleRequiredAsync(bt => bt.Id == request.TierId && bt.ClubId == request.ClubId, cancellationToken);

            if (await context.MemberBadges.AnyAsync(mb => mb.BadgeTierId == tier.Id, cancellationToken))
                throw new PreconditionException(ErrorCode.BadgeTierAlreadyEarned, "Cannot delete a badge tier that a member has already earned.");

            context.BadgeTiers.Remove(tier);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
