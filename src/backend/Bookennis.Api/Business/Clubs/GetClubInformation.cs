using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Club;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record GetClubInformation(int ClubId) : IQuery<ClubInformationResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubInformation, ClubInformationResult>
    {
        public async Task<ClubInformationResult> Handle(GetClubInformation request, CancellationToken cancellationToken)
        {
            var clubInfo = await context.Clubs
                                        .Select(c => new ClubInformationResult
                                        {
                                            OpeningHours = c.OpeningHours,
                                            PrimeTimeSettings = new PrimeTimeSettingsDto
                                            {
                                                IsEnabled = c.PrimeTimeSettings.IsEnabled,
                                                PrimeTimeHours = c.PrimeTimeSettings.PrimeTimeHours,
                                                ApplicableWeekdays = c.PrimeTimeSettings.ApplicableWeekdays,
                                                RestrictChildren = c.PrimeTimeSettings.RestrictChildren,
                                                RestrictGuests = c.PrimeTimeSettings.RestrictGuests,
                                                ChildAgeThreshold = c.PrimeTimeSettings.ChildAgeThreshold
                                            },
                                            BookingGracePeriodInMinutes = c.BookingGracePeriodInMinutes
                                        })
                                        .AsNoTracking()
                                        .SingleRequiredAsync(cancellationToken);

            return clubInfo;
        }
    }
}