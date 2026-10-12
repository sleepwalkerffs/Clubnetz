using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>Sets the order in which members see the plans of a season. Plans that are not listed keep their place behind the listed ones.</summary>
public record SortMembershipPlans(int ClubId, int SeasonId, IReadOnlyList<int> PlanIds) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<SortMembershipPlans>
    {
        public async Task<Unit> Handle(SortMembershipPlans request, CancellationToken cancellationToken)
        {
            var plans = await context.MembershipPlans
                .Where(p => p.ClubId == request.ClubId && p.SeasonId == request.SeasonId)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);

            var requested = request.PlanIds.Distinct().ToList();
            var sorted = plans
                .OrderBy(p => requested.IndexOf(p.Id) is var index and >= 0 ? index : int.MaxValue)
                .ToList();

            for (var i = 0; i < sorted.Count; i++)
                sorted[i].SetSortOrder(i + 1);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
