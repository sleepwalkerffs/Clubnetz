using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Wrapped;

/// <summary>Takes the Wrapped of a season back: members can no longer open it, the club admins can still preview it.</summary>
public record UnpublishSeasonWrapped(int ClubId, int SeasonId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UnpublishSeasonWrapped>
    {
        public async Task<Unit> Handle(UnpublishSeasonWrapped request, CancellationToken cancellationToken)
        {
            var season = await context.Seasons.SingleRequiredAsync(s => s.Id == request.SeasonId && s.ClubId == request.ClubId, cancellationToken);

            season.UnpublishWrapped();

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
