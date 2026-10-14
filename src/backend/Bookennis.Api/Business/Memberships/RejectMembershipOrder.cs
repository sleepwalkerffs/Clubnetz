using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>The treasurer declines a plan that needs a confirmation. The order is cancelled and the member can choose another plan.</summary>
public record RejectMembershipOrder(int ClubId, int MembershipOrderId) : ICommand<MembershipOrderDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<RejectMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(RejectMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            order.Reject();

            await context.SaveChangesAsync(cancellationToken);
            return await order.ToDto(context, cancellationToken);
        }
    }
}
