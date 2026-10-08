using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record UpdateOneTimeBadge(int ClubId, int OneTimeBadgeId, string Name, string Description) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateOneTimeBadge>
    {
        public async Task<Unit> Handle(UpdateOneTimeBadge request, CancellationToken cancellationToken)
        {
            var badge = await context.OneTimeBadges
                .SingleRequiredAsync(b => b.Id == request.OneTimeBadgeId && b.ClubId == request.ClubId, cancellationToken);

            badge.Update(request.Name, request.Description);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
