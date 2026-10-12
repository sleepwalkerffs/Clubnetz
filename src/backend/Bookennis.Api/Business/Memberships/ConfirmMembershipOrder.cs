using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>The member confirms a free plan (e.g. "children up to 8"): there is nothing to pay, the members are enrolled right away.</summary>
public record ConfirmMembershipOrder(int ClubId, int MembershipOrderId) : ICommand<MembershipOrderDto>
{
    public enum ErrorCode
    {
        /// <summary>Only a free plan can be confirmed without a payment.</summary>
        MembershipOrderNotFree = 0,
    }

    public class Handler(AppDbContext context, IMembershipFulfillment fulfillment) : IRequestHandler<ConfirmMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(ConfirmMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            if (!order.IsFree)
                throw new PreconditionException(ErrorCode.MembershipOrderNotFree, "This plan has to be paid.");

            // A plan that needs a confirmation is confirmed by the treasurer first
            if (order.Status == Domain.Memberships.MembershipOrderStatus.AwaitingApproval)
                throw new PreconditionException(Domain.Memberships.MembershipOrder.ErrorCode.MembershipOrderInvalidStatus, [order.Status.ToString()], "The club has to confirm this plan first.");

            var wasPaid = order.Status == Domain.Memberships.MembershipOrderStatus.Paid;
            order.ConfirmFree(DateTimeOffset.UtcNow);
            if (!wasPaid)
                await fulfillment.Complete(order, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return await order.ToDto(context, cancellationToken);
        }
    }
}
