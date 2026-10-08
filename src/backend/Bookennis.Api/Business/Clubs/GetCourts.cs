using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Club;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record GetCourts : ICommand<GetCourtsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCourts, GetCourtsResult>
    {
        public async Task<GetCourtsResult> Handle(GetCourts request, CancellationToken cancellationToken)
        {
            var courts = await context.Courts
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .Select(c => new CourtResult()
                {
                    CourtId = c.Id,
                    CulbId = c.ClubId,
                    Name = c.Name,
                    Inactive = c.Inactive,
                    Alias = c.Alias,
                    SortOrder = c.SortOrder
                }).ToListAsync(cancellationToken);

            return new GetCourtsResult() { Courts = courts };
        }
    }
}
