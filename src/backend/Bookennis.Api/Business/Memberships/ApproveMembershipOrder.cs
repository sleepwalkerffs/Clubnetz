using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>The treasurer confirms that the member may have the plan (student, apprentice, ...). The member is told by email and can pay.</summary>
public record ApproveMembershipOrder(int ClubId, int MembershipOrderId) : ICommand<MembershipOrderDto>
{
    public class Handler(AppDbContext context, IMembershipNotifier notifier) : IRequestHandler<ApproveMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(ApproveMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);
            order.Approve(DateTimeOffset.UtcNow);

            await context.SaveChangesAsync(cancellationToken);
            await notifier.Approved(order, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }
    }
}
