using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminDeletePlayMode(int ClubId, int PlayModeId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminDeletePlayMode>
    {
        public async Task<Unit> Handle(AdminDeletePlayMode request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.Include(c => c.PlayModes).SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken)
                ?? throw new EntityNotFoundException(typeof(Club), request.ClubId);

            club.RemovePlayMode(request.PlayModeId);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
