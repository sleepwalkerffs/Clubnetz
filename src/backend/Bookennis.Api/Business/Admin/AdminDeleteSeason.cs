using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminDeleteSeason(int ClubId, int SeasonId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminDeleteSeason>
    {
        public async Task<Unit> Handle(AdminDeleteSeason request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.Seasons)
                .SingleRequiredAsync(c => c.Id == request.ClubId, cancellationToken);

            club.RemoveSeason(request.SeasonId);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
