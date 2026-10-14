using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Memberships;

/// <summary>
/// The membership of the signed-in member: choose a plan for oneself or, as a parent, for the family, and pay it.
/// An order is only open to the people it is about (<see cref="AuthorizationPolicies.OwnsMembershipOrder"/>): its payer,
/// the members it covers and the parents of a covered child. Treasurers manage all orders with <c>MembershipPayments</c>.
/// </summary>
[Authorize(AuthorizationPolicies.ClubMember)]
public class MembershipsController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    /// <summary>The plans of a season with who they can be ordered for, and the orders that exist. Without a season: the running one, else the next.</summary>
    [HttpGet("options")]
    public Task<GetMembershipOptionsResult> GetMembershipOptions(int clubId, [FromQuery] int? seasonId, CancellationToken cancellationToken)
        => mediator.Send(new GetMembershipOptions(clubId, userAccessor.GetUserId(), seasonId), cancellationToken);

    /// <summary>Orders a plan for oneself or for members of the own family. The rules of the plan are checked.</summary>
    [HttpPost("orders")]
    public Task<MembershipOrderDto> CreateMembershipOrder(int clubId, [FromBody] CreateMembershipOrderModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateMembershipOrder(clubId, userAccessor.GetUserId(), model.MembershipPlanId, model.MemberIds ?? []), cancellationToken);

    /// <summary>An order of the caller. While it waits for a payment whose page was opened, Stripe is asked how the payment went.</summary>
    [HttpGet("orders/{orderId:int}")]
    public async Task<MembershipOrderDto> GetMembershipOrder(int clubId, int orderId, CancellationToken cancellationToken)
    {
        await EnsureOwnsOrder(orderId);
        await mediator.Send(new SyncMembershipCheckout(clubId, orderId), cancellationToken);
        return await mediator.Send(new GetMembershipOrder(clubId, orderId), cancellationToken);
    }

    /// <summary>Returns the address of the payment page for the order. The result of the payment is reported to the app by the payment provider.</summary>
    [HttpPost("orders/{orderId:int}/checkout")]
    public async Task<StartMembershipCheckoutResult> StartMembershipCheckout(int clubId, int orderId, CancellationToken cancellationToken)
    {
        await EnsureOwnsOrder(orderId);
        return await mediator.Send(new StartMembershipCheckout(clubId, userAccessor.GetUserId(), orderId), cancellationToken);
    }

    /// <summary>Confirms an order of a free plan: there is nothing to pay, the members are enrolled.</summary>
    [HttpPost("orders/{orderId:int}/confirm")]
    public async Task<MembershipOrderDto> ConfirmMembershipOrder(int clubId, int orderId, CancellationToken cancellationToken)
    {
        await EnsureOwnsOrder(orderId);
        return await mediator.Send(new ConfirmMembershipOrder(clubId, orderId), cancellationToken);
    }

    /// <summary>Withdraws an order that was not paid.</summary>
    [HttpDelete("orders/{orderId:int}")]
    public async Task<MembershipOrderDto> CancelMembershipOrder(int clubId, int orderId, CancellationToken cancellationToken)
    {
        await EnsureOwnsOrder(orderId);
        return await mediator.Send(new CancelMembershipOrder(clubId, orderId, ByTreasurer: false), cancellationToken);
    }

    private Task EnsureOwnsOrder(int orderId)
        => authorizationService.AuthorizeAsync(HttpContext.User, new MembershipOrderAuthorizationModel(orderId), AuthorizationPolicies.OwnsMembershipOrder).EnsureSucceeded();
}
