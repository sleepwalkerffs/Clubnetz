using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record GetAdminClubSeasons(int ClubId) : IQuery<List<AdminSeasonResult>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClubSeasons, List<AdminSeasonResult>>
    {
        public async Task<List<AdminSeasonResult>> Handle(GetAdminClubSeasons request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await context.Seasons
                .Where(s => s.ClubId == request.ClubId)
                .OrderByDescending(s => s.Period.From)
                .Select(s => new AdminSeasonResult
                {
                    Id = s.Id,
                    ClubId = s.ClubId,
                    StartDate = s.Period.From,
                    EndDate = s.Period.To,
                    IsActive = s.Period.From <= today && today <= s.Period.To
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
