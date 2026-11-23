using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// Changes a plan. Orders that exist keep the name, amount and sports they were created with, with one exception: a plan for
/// every sport that is narrowed (a club that adds padel courts turns its plan into a tennis plan) narrows its orders for every
/// sport too. They were bought when the club had one sport, so its members can then buy the new sport. Their enrollments stay
/// as they are: the treasurer narrows them on the member page.
/// </summary>
public record UpdateMembershipPlan(int ClubId, int MembershipPlanId, MembershipPlan.PlanData Data) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateMembershipPlan>
    {
        public async Task<Unit> Handle(UpdateMembershipPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.MembershipPlans.SingleRequiredAsync(p => p.Id == request.MembershipPlanId && p.ClubId == request.ClubId, cancellationToken);

            var wasForEverySport = plan.Sports.Length == 0;
            plan.Update(request.Data);

            if (wasForEverySport && plan.Sports.Length > 0)
            {
                var orders = await context.MembershipOrders
                    .Where(o => o.MembershipPlanId == plan.Id && o.ClubId == plan.ClubId && o.Sports.Length == 0)
                    .ToListAsync(cancellationToken);
                foreach (var order in orders)
                    order.NarrowSports(plan.Sports);
            }

            await context.EnsurePlanNameIsFree(plan, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
