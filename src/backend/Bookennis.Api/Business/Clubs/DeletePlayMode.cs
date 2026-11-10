using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record DeletePlayMode(int PlayModeId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeletePlayMode>
    {
        public async Task<Unit> Handle(DeletePlayMode request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.Include(c => c.PlayModes).SingleRequiredAsync(cancellationToken);

            club.RemovePlayMode(request.PlayModeId);
            await Permissions.PermissionGrantCleanup.RemoveForResource(context, club.Id, Bookennis.Domain.Permissions.PermissionResourceType.PlayMode, request.PlayModeId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
