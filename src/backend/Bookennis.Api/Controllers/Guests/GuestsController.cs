using Bookennis.Api.Business.Guests;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Guests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Guests;

[Authorize(AuthorizationPolicies.ClubTreasurer)]
public class GuestsController(IMediator mediator) : ClubControllerBase
{
    [HttpPost]
    public async Task<CreateGuestCardResult> CreateGuestCard(CreateGuestCardRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new CreateGuestCard(
            request.PurchasedBookings,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Birthday,
            (Gender)request.Gender), cancellationToken);

    [HttpGet]
    public async Task<GetGuestCardResult> GetGuestCards(CancellationToken cancellationToken)
        => await mediator.Send(new GetGuestCards(), cancellationToken);

    [HttpDelete("{id:int}")]
    public async Task DeleteGuestCard(int id, CancellationToken cancellationToken)
        => await mediator.Send(new DeleteGuestCard(id), cancellationToken);
}
