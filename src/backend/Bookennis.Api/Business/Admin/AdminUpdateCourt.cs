using Bookennis.Api.Data;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminUpdateCourt(int ClubId, int CourtId, string Name, string Alias, int SortOrder, DateTimeOffsetInterval? Inactive) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminUpdateCourt>
    {
        public async Task<Unit> Handle(AdminUpdateCourt request, CancellationToken cancellationToken)
        {
            var court = await context.Courts.SingleRequiredAsync(x => x.Id == request.CourtId && x.ClubId == request.ClubId, cancellationToken);

            court.RenameCourt(request.Name);
            court.RenameCourtAlias(request.Alias);
            court.Order(request.SortOrder);

            if (request.Inactive is null)
                court.ReactivateCourt();
            else
                court.SetInactive(request.Inactive);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
