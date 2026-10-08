using Bookennis.Api.Data;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record UpdateCourt(int CourtId, string Name, string Alias, DateTimeOffsetInterval? Inactive, int SortOrder) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateCourt>
    {
        public async Task<Unit> Handle(UpdateCourt request, CancellationToken cancellationToken)
        {
            var court = await context.Courts.SingleRequiredAsync(x => x.Id == request.CourtId, cancellationToken);

            court.RenameCourt(request.Name);
            court.RenameCourtAlias(request.Alias);
            court.Order(request.SortOrder);
            if (request.Inactive is null)
            {
                court.ReactivateCourt();
            }
            else
            {
                court.SetInactive(request.Inactive);
            }

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
