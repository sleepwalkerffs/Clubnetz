using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Profile;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

public record GetAvailableClubs : IQuery<GetAvailableClubsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAvailableClubs, GetAvailableClubsResult>
    {
        public async Task<GetAvailableClubsResult> Handle(GetAvailableClubs request, CancellationToken cancellationToken)
        {
            var clubs = await context.Clubs
                .IgnoreQueryFilters()
                .Select(c => new AvailableClubDto
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new GetAvailableClubsResult { Clubs = clubs };
        }
    }
}
