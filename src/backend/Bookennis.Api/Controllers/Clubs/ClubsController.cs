using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Domain.Clubs;
using Bookennis.Shared.Controller.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.Member)]
public class ClubsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{clubId:int}")]
    [Authorize(AuthorizationPolicies.Member)]
    public Task<ClubInformationResult> GetClub(int clubId, CancellationToken cancellationToken) => mediator.Send(new GetClubInformation(clubId), cancellationToken);

    [HttpPost("{clubId:int}")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public Task UpdateClubInformation(int clubId, ClubInformationModel model, CancellationToken cancellationToken)
        => mediator.Send(
            new UpdateClubInformation(
                clubId,
                model.OpeningHours,
                new PrimeTimeSettings(
                    model.PrimeTimeSettings.IsEnabled,
                    model.PrimeTimeSettings.PrimeTimeHours,
                    model.PrimeTimeSettings.ApplicableWeekdays,
                    model.PrimeTimeSettings.RestrictChildren,
                    model.PrimeTimeSettings.RestrictGuests,
                    model.PrimeTimeSettings.ChildAgeThreshold),
                model.BookingGracePeriodInMinutes),
            cancellationToken);
}