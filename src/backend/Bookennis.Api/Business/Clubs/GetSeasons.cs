using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Club;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record GetSeasons() : IQuery<GetSeasonsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetSeasons, GetSeasonsResult>
    {
        public async Task<GetSeasonsResult> Handle(GetSeasons request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var seasons = await context.Seasons
                .OrderByDescending(s => s.Period.From)
                .Select(s => new SeasonResult
                {
                    Id = s.Id,
                    StartDate = s.Period.From,
                    EndDate = s.Period.To,
                    IsActive = s.Period.From <= today && today <= s.Period.To
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new GetSeasonsResult { Seasons = seasons };
        }
    }
}
