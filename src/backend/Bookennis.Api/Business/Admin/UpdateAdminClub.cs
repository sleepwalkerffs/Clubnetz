using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record UpdateAdminClub(int ClubId, string Name, TimeOnlyInterval OpeningHours, TimeOnlyInterval PrimeTimeHours, PrimeTimeSettings PrimeTimeSettings, int? BookingGracePeriodInMinutes, bool IsAtpClub) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateAdminClub>
    {
        public async Task<Unit> Handle(UpdateAdminClub request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.Clubs.Club), request.ClubId);

            club.Update(request.Name, request.OpeningHours, request.PrimeTimeHours, request.BookingGracePeriodInMinutes, request.IsAtpClub, request.PrimeTimeSettings);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
