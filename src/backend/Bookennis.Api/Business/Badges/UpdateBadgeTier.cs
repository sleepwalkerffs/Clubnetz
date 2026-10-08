using Bookennis.Api.Data;
using Bookennis.Shared.Controller.BadgeTiers;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record UpdateBadgeTier(int ClubId, int TierId, UpdateBadgeTierModel Model) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateBadgeTier>
    {
        public async Task<Unit> Handle(UpdateBadgeTier request, CancellationToken cancellationToken)
        {
            var tier = await context.BadgeTiers
                .SingleRequiredAsync(bt => bt.Id == request.TierId && bt.ClubId == request.ClubId, cancellationToken);

            tier.Update(request.Model.Name, request.Model.Description, request.Model.MatchesRequired);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
