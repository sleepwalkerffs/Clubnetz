using Bookennis.Api.Business.Permissions;
using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminDeleteCourt(int ClubId, int CourtId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminDeleteCourt>
    {
        public async Task<Unit> Handle(AdminDeleteCourt request, CancellationToken cancellationToken)
        {
            var court = await context.Courts.SingleRequiredAsync(x => x.Id == request.CourtId && x.ClubId == request.ClubId, cancellationToken);
            context.Courts.Remove(court);
            // Play modes limited to this court no longer list it
            var playModes = await context.PlayModes.IgnoreQueryFilters()
                .Where(p => p.ClubId == request.ClubId && p.CourtIds.Contains(request.CourtId))
                .ToListAsync(cancellationToken);
            foreach (var playMode in playModes)
                playMode.RestrictToCourts(playMode.CourtIds.Where(id => id != request.CourtId));

            await PermissionGrantCleanup.RemoveForResource(context, request.ClubId, Bookennis.Domain.Permissions.PermissionResourceType.Court, request.CourtId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
