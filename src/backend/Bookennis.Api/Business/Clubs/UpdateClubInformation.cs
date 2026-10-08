using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record UpdateClubInformation(int ClubId, TimeOnlyInterval OpeningHours, PrimeTimeSettings PrimeTimeSettings, int? BookingGracePeriodInMinutes) : ICommand
{
    public enum ErrorCode
    {
        PrimeTimeMustBeWithinClubOpening = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<UpdateClubInformation>
    {
        public async Task<Unit> Handle(UpdateClubInformation request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.SingleRequiredAsync(cancellationToken);
            if (!request.OpeningHours.Contains(request.PrimeTimeSettings.PrimeTimeHours))
                throw new PreconditionException(ErrorCode.PrimeTimeMustBeWithinClubOpening, "Prime time hours must be within club opening hours");

            club.UpdateWorkingHours(request.OpeningHours, request.PrimeTimeSettings.PrimeTimeHours);
            club.SetBookingGracePeriod(request.BookingGracePeriodInMinutes);
            club.UpdatePrimeTimeSettings(request.PrimeTimeSettings);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
