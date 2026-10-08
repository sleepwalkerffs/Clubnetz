using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record DeleteSeason(int SeasonId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteSeason>
    {
        public async Task<Unit> Handle(DeleteSeason request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.Seasons)
                .SingleRequiredAsync(cancellationToken);

            club.RemoveSeason(request.SeasonId);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
