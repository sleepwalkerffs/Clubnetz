using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record AddSeason(DateOnly StartDate, DateOnly EndDate) : ICommand<int>
{
    public enum ErrorCode
    {
        SeasonsCannotOverlap = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<AddSeason, int>
    {
        public async Task<int> Handle(AddSeason request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.Seasons)
                .SingleRequiredAsync(cancellationToken);

            var period = new DateOnlyInterval(request.StartDate, request.EndDate);

            if (club.Seasons.Any(s => s.Period.Intersects(period)))
                throw new PreconditionException(ErrorCode.SeasonsCannotOverlap, "Seasons cannot overlap.");

            var season = club.AddSeason(period);

            await context.SaveChangesAsync(cancellationToken);
            return season.Id;
        }
    }
}
