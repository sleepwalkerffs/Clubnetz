using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// Cancels an order. A member can withdraw an order as long as nothing was paid; the treasurer (<c>ByTreasurer</c>) can also cancel
/// a paid one, e.g. to record another plan. Enrollments are never removed by this, and money is given back outside of Clubnetz.
/// </summary>
public record CancelMembershipOrder(int ClubId, int MembershipOrderId, bool ByTreasurer) : ICommand<MembershipOrderDto>
{
    public class Handler(AppDbContext context, IMembershipCheckout checkout) : IRequestHandler<CancelMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(CancelMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            var openCheckoutSessionId = order.StripeCheckoutSessionId;

            if (request.ByTreasurer)
                order.CancelByTreasurer();
            else
                order.CancelByMember();

            await context.SaveChangesAsync(cancellationToken);

            // The payment page that may still be open in the browser of the member must not take money for a cancelled order
            if (openCheckoutSessionId is not null)
                await checkout.ExpireSession(order.ClubId, openCheckoutSessionId, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }
    }
}
