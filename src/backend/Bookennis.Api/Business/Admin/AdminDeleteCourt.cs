using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminDeleteCourt(int ClubId, int CourtId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminDeleteCourt>
    {
        public async Task<Unit> Handle(AdminDeleteCourt request, CancellationToken cancellationToken)
        {
            var court = await context.Courts.SingleRequiredAsync(x => x.Id == request.CourtId && x.ClubId == request.ClubId, cancellationToken);
            context.Courts.Remove(court);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
