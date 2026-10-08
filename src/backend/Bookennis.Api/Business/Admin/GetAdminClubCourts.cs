using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record GetAdminClubCourts(int ClubId) : IQuery<List<AdminCourtResult>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClubCourts, List<AdminCourtResult>>
    {
        public async Task<List<AdminCourtResult>> Handle(GetAdminClubCourts request, CancellationToken cancellationToken)
        {
            return await context.Courts
                .AsNoTracking()
                .Where(c => c.ClubId == request.ClubId)
                .OrderBy(c => c.SortOrder)
                .Select(c => new AdminCourtResult
                {
                    CourtId = c.Id,
                    ClubId = c.ClubId,
                    Name = c.Name,
                    Alias = c.Alias,
                    SortOrder = c.SortOrder,
                    Inactive = c.Inactive
                })
                .ToListAsync(cancellationToken);
        }
    }
}
