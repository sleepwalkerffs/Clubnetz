using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Controllers.Memberships;

/// <summary>
/// The treasurer's view of the membership fees of a season: who has which plan, who has paid, and recording payments that
/// arrived outside of the app. For Treasurers and Admins. The lists carry names, plan and payment - no email, birthday or address.
/// </summary>
[Authorize(AuthorizationPolicies.ClubTreasurer)]
public class MembershipPaymentsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    /// <summary>Every club member with how they got into the season, and the orders of the season (optionally only those in one status).</summary>
    [HttpGet]
    public Task<GetMembershipPaymentsResult> GetMembershipPayments(int clubId, [FromQuery] int seasonId, [FromQuery] MembershipOrderStatus? status, CancellationToken cancellationToken)
        => mediator.Send(new GetMembershipPayments(clubId, seasonId, status), cancellationToken);

    [HttpGet("summary")]
    public Task<GetMembershipPaymentsSummaryResult> GetMembershipPaymentsSummary(int clubId, [FromQuery] int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new GetMembershipPaymentsSummary(clubId, seasonId), cancellationToken);

    /// <summary>Records a fee that was paid by bank transfer or cash for members without an order: creates a paid order and enrolls them.</summary>
    [HttpPost]
    public Task<MembershipOrderDto> RecordMembershipPayment(int clubId, [FromBody] RecordMembershipPaymentModel model, CancellationToken cancellationToken)
        => mediator.Send(
            new RecordMembershipPayment(clubId, userAccessor.GetUserId(), model.MembershipPlanId, model.MemberIds ?? [], model.PayerMemberId, (MembershipPaymentMethod)model.Method, model.PaidOn, model.Note),
            cancellationToken);

    /// <summary>
    /// Confirms an order of a plan that needs a confirmation. The member is told by email and can pay.
    /// The body is optional: with <c>memberIds</c> the order covers these members from now on.
    /// </summary>
    [HttpPost("{orderId:int}/approve")]
    public Task<MembershipOrderDto> ApproveMembershipOrder(int clubId, int orderId, [FromBody] ApproveMembershipOrderModel? model, CancellationToken cancellationToken)
        => mediator.Send(new ApproveMembershipOrder(clubId, orderId, model?.MemberIds), cancellationToken);

    [HttpPost("{orderId:int}/reject")]
    public Task<MembershipOrderDto> RejectMembershipOrder(int clubId, int orderId, CancellationToken cancellationToken)
        => mediator.Send(new RejectMembershipOrder(clubId, orderId), cancellationToken);

    /// <summary>Marks an open order as paid outside of the app and enrolls its members.</summary>
    [HttpPost("{orderId:int}/mark-paid")]
    public Task<MembershipOrderDto> MarkMembershipOrderPaid(int clubId, int orderId, [FromBody] MarkMembershipOrderPaidModel model, CancellationToken cancellationToken)
        => mediator.Send(new MarkMembershipOrderPaid(clubId, userAccessor.GetUserId(), orderId, (MembershipPaymentMethod)model.Method, model.PaidOn, model.Note), cancellationToken);

    /// <summary>Cancels an order, also a paid one. Enrollments stay, and money is given back outside of the app.</summary>
    [HttpPost("{orderId:int}/cancel")]
    public Task<MembershipOrderDto> CancelMembershipOrder(int clubId, int orderId, CancellationToken cancellationToken)
        => mediator.Send(new CancelMembershipOrder(clubId, orderId, ByTreasurer: true), cancellationToken);

    /// <summary>The list as an Excel workbook, in the language of the request.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportMembershipPayments(int clubId, [FromQuery] int seasonId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportMembershipPayments(clubId, seasonId), cancellationToken);
        return File(result.Data, ExportMembershipPaymentsResult.ContentType, result.FileName);
    }
}
