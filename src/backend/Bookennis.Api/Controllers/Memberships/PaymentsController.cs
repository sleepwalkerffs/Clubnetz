using Bookennis.Api.Business.Payments;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Memberships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Memberships;

/// <summary>
/// Online payment of the club: whether it is available and the status of the club's own Stripe account, to which the membership fees
/// are paid directly. For Treasurers and Admins. Connecting and disconnecting the account is not part of this API.
/// </summary>
public class PaymentsController(IMediator mediator) : ClubControllerBase
{
    /// <summary>Whether this installation offers online payment and whether the club is connected.</summary>
    [HttpGet("configuration")]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task<PaymentConfigurationResult> GetPaymentConfiguration(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetPaymentConfiguration(clubId), cancellationToken);

    [HttpGet("account")]
    [Authorize(AuthorizationPolicies.ClubTreasurer)]
    public Task<PaymentAccountDto> GetPaymentAccount(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetPaymentAccount(clubId), cancellationToken);
}
