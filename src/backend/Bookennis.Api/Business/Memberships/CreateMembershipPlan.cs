using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

public record CreateMembershipPlan(int ClubId, int SeasonId, MembershipPlan.PlanData Data) : ICommand<int>
{
    public enum ErrorCode
    {
        MembershipPlanNameAlreadyUsed = 0,
    }

    public class Handler(AppDbContext context) : IRequestHandler<CreateMembershipPlan, int>
    {
        public async Task<int> Handle(CreateMembershipPlan request, CancellationToken cancellationToken)
        {
            // Only a season of this club
            var season = await context.Seasons.SingleRequiredAsync(s => s.Id == request.SeasonId && s.ClubId == request.ClubId, cancellationToken);

            var lastSortOrder = await context.MembershipPlans
                .Where(p => p.ClubId == request.ClubId && p.SeasonId == season.Id)
                .MaxAsync(p => (int?)p.SortOrder, cancellationToken) ?? 0;

            var plan = new MembershipPlan(request.ClubId, season.Id, request.Data, lastSortOrder + 1);
            await context.EnsurePlanNameIsFree(plan, cancellationToken);

            context.MembershipPlans.Add(plan);
            await context.SaveChangesAsync(cancellationToken);

            return plan.Id;
        }
    }
}

public static class MembershipPlanQueryExtensions
{
    public static async Task EnsurePlanNameIsFree(this AppDbContext context, MembershipPlan plan, CancellationToken cancellationToken)
    {
        var nameAlreadyUsed = await context.MembershipPlans
            .AnyAsync(p => p.ClubId == plan.ClubId && p.SeasonId == plan.SeasonId && p.Id != plan.Id && p.Name == plan.Name, cancellationToken);

        if (nameAlreadyUsed)
            throw new PreconditionException(CreateMembershipPlan.ErrorCode.MembershipPlanNameAlreadyUsed, "A plan with this name already exists in the season.");
    }
}
