using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>An order as its members see it. The page polls it after coming back from the payment page.</summary>
public record GetMembershipOrder(int ClubId, int MembershipOrderId) : IQuery<MembershipOrderDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(GetMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            return await order.ToDto(context, cancellationToken);
        }
    }
}
