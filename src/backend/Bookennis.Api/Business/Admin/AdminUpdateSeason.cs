using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminUpdateSeason(int ClubId, int SeasonId, DateOnly StartDate, DateOnly EndDate) : ICommand
{
    public enum ErrorCode
    {
        SeasonsCannotOverlap = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<AdminUpdateSeason>
    {
        public async Task<Unit> Handle(AdminUpdateSeason request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.Seasons)
                .SingleRequiredAsync(c => c.Id == request.ClubId, cancellationToken);

            var period = new DateOnlyInterval(request.StartDate, request.EndDate);

            if (club.Seasons.Any(s => s.Id != request.SeasonId && s.Period.Intersects(period)))
                throw new PreconditionException(ErrorCode.SeasonsCannotOverlap, "Seasons cannot overlap.");

            club.UpdateSeason(request.SeasonId, period);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
