using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record DeleteAdminClub(int ClubId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteAdminClub>
    {
        public async Task<Unit> Handle(DeleteAdminClub request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.PlayModes)
                .SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.Clubs.Club), request.ClubId);

            // Remove associated members
            var members = await context.ClubMembers.Where(m => m.ClubId == request.ClubId).ToListAsync(cancellationToken);
            context.ClubMembers.RemoveRange(members);

            context.Clubs.Remove(club);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
