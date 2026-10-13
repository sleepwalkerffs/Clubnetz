using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// The treasurer confirms that the member may have the plan (student, apprentice, ...). The member is told by email and can pay.
/// With <paramref name="MemberIds"/> the treasurer also sets who the order covers, e.g. the family that was put together for a family plan.
/// See <see cref="MembershipPayments.SetMembers"/> for what is checked.
/// </summary>
public record ApproveMembershipOrder(int ClubId, int MembershipOrderId, IReadOnlyCollection<int>? MemberIds = null) : ICommand<MembershipOrderDto>
{
    public class Handler(AppDbContext context, IMembershipNotifier notifier) : IRequestHandler<ApproveMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(ApproveMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);

            if (request.MemberIds is not null)
                await MembershipPayments.SetMembers(context, order, request.MemberIds, cancellationToken);

            order.Approve(DateTimeOffset.UtcNow);

            await context.SaveChangesAsync(cancellationToken);
            await notifier.Approved(order, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }
    }
}
