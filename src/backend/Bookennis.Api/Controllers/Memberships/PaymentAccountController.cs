using Bookennis.Api.Business.Payments;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Memberships;
using Bookennis.Shared.Controller.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Memberships;

/// <summary>
/// Connects the club with its Stripe account and disconnects it. Where the money of the club goes is decided here, so these
/// endpoints only accept a signed-in Admin of the club: not a treasurer, and never an API key.
/// </summary>
[Route("api/Clubs/{clubId:int}/Payments/account")]
[Authorize(AuthorizationPolicies.ClubPaymentAccountManager)]
public class PaymentAccountController(IMediator mediator) : ClubControllerBase
{
    [HttpPost("connect")]
    public Task<ConnectPaymentAccountResult> ConnectPaymentAccount(int clubId, [FromBody] ConnectPaymentAccountModel model, CancellationToken cancellationToken)
        => mediator.Send(new ConnectPaymentAccount(clubId, ToCountryCode(model.Country)), cancellationToken);

    [HttpPost("refresh")]
    public Task<PaymentAccountDto> RefreshPaymentAccount(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new RefreshPaymentAccount(clubId), cancellationToken);

    [HttpDelete]
    public Task DisconnectPaymentAccount(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new DisconnectPaymentAccount(clubId), cancellationToken);

    private static string ToCountryCode(Country country) => country switch
    {
        Country.Germany => "DE",
        Country.Switzerland => "CH",
        _ => "AT",
    };
}
