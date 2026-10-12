using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// The price list of a season. Members get the plans they can choose themselves;
/// treasurers (<c>CanManage</c>) also get the ones only they can assign and the number of orders per plan.
/// </summary>
public record GetMembershipPlans(int ClubId, int SeasonId, bool CanManage) : IQuery<GetMembershipPlansResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMembershipPlans, GetMembershipPlansResult>
    {
        public async Task<GetMembershipPlansResult> Handle(GetMembershipPlans request, CancellationToken cancellationToken)
        {
            var plans = await context.MembershipPlans
                .AsNoTracking()
                .Where(p => p.ClubId == request.ClubId && p.SeasonId == request.SeasonId && (request.CanManage || p.IsSelfService))
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .Select(p => new
                {
                    Plan = p,
                    OrderCount = context.MembershipOrders.Count(o => o.MembershipPlanId == p.Id),
                })
                .ToListAsync(cancellationToken);

            return new GetMembershipPlansResult
            {
                Plans = plans.ConvertAll(p => p.Plan.ToDto(request.CanManage ? p.OrderCount : 0)),
            };
        }
    }
}
