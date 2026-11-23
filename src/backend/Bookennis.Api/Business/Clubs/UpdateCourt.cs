using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record UpdateCourt(int CourtId, string Name, string Alias, DateTimeOffsetInterval? Inactive, int SortOrder, bool? IsIndoor = null, Sport? Sport = null) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateCourt>
    {
        public async Task<Unit> Handle(UpdateCourt request, CancellationToken cancellationToken)
        {
            var court = await context.Courts.SingleRequiredAsync(x => x.Id == request.CourtId, cancellationToken);

            court.RenameCourt(request.Name);
            court.RenameCourtAlias(request.Alias);
            court.Order(request.SortOrder);
            // Not sent by older clients and scripts: the court stays as it is
            if (request.IsIndoor is { } isIndoor)
                court.SetIndoor(isIndoor);
            // Existing bookings stay when the sport changes; new ones follow the enrollments for the new sport
            if (request.Sport is { } sport)
                court.SetSport(sport);
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
