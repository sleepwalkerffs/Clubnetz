using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Wrapped;

/// <summary>Releases the Wrapped of a season (also a past one) to the members of the club.</summary>
public record PublishSeasonWrapped(int ClubId, int SeasonId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<PublishSeasonWrapped>
    {
        public async Task<Unit> Handle(PublishSeasonWrapped request, CancellationToken cancellationToken)
        {
            var season = await context.Seasons.SingleRequiredAsync(s => s.Id == request.SeasonId && s.ClubId == request.ClubId, cancellationToken);

            season.PublishWrapped(DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow));

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
