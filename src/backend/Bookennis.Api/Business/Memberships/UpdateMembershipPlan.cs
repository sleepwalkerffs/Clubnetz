using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>Changes a plan. Orders that exist keep the name and amount they were created with.</summary>
public record UpdateMembershipPlan(int ClubId, int MembershipPlanId, MembershipPlan.PlanData Data) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateMembershipPlan>
    {
        public async Task<Unit> Handle(UpdateMembershipPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.MembershipPlans.SingleRequiredAsync(p => p.Id == request.MembershipPlanId && p.ClubId == request.ClubId, cancellationToken);

            plan.Update(request.Data);
            await context.EnsurePlanNameIsFree(plan, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
