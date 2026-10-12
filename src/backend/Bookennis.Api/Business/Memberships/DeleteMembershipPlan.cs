using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

public record DeleteMembershipPlan(int ClubId, int MembershipPlanId) : ICommand
{
    public enum ErrorCode
    {
        /// <summary>The orders of a plan are the club's record of who chose and paid what. Take the plan out of self-service instead.</summary>
        MembershipPlanHasOrders = 0,
    }

    public class Handler(AppDbContext context) : IRequestHandler<DeleteMembershipPlan>
    {
        public async Task<Unit> Handle(DeleteMembershipPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.MembershipPlans.SingleRequiredAsync(p => p.Id == request.MembershipPlanId && p.ClubId == request.ClubId, cancellationToken);

            if (await context.MembershipOrders.AnyAsync(o => o.MembershipPlanId == plan.Id, cancellationToken))
                throw new PreconditionException(ErrorCode.MembershipPlanHasOrders, "A plan that has orders can't be deleted.");

            context.MembershipPlans.Remove(plan);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
