using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record GetAdminClubs : IQuery<List<AdminClubResult>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClubs, List<AdminClubResult>>
    {
        public async Task<List<AdminClubResult>> Handle(GetAdminClubs request, CancellationToken cancellationToken) =>
            await context.Clubs
                .Select(c => new AdminClubResult
                {
                    Id = c.Id,
                    Name = c.Name,
                    MemberCount = context.ClubMembers.Count(m => m.ClubId == c.Id),
                    PlayModeCount = c.PlayModes.Count
                })
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
    }
}
