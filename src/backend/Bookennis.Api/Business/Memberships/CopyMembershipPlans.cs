using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// Copies the price list of a season into another one, as the starting point for next year's prices.
/// Birth years and availability dates move by the difference of the season years: "born 2009 to 2013" in 2026 becomes "born 2010 to 2014" in 2027.
/// </summary>
public record CopyMembershipPlans(int ClubId, int SourceSeasonId, int TargetSeasonId) : ICommand
{
    public enum ErrorCode
    {
        MembershipPlanSourceSeasonEmpty = 0,
        MembershipPlanTargetSeasonNotEmpty = 1,
    }

    public class Handler(AppDbContext context) : IRequestHandler<CopyMembershipPlans>
    {
        public async Task<Unit> Handle(CopyMembershipPlans request, CancellationToken cancellationToken)
        {
            var sourceSeason = await context.Seasons.SingleRequiredAsync(s => s.Id == request.SourceSeasonId && s.ClubId == request.ClubId, cancellationToken);
            var targetSeason = await context.Seasons.SingleRequiredAsync(s => s.Id == request.TargetSeasonId && s.ClubId == request.ClubId, cancellationToken);

            var sourcePlans = await context.MembershipPlans
                .Where(p => p.ClubId == request.ClubId && p.SeasonId == sourceSeason.Id)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);

            if (sourcePlans.Count == 0)
                throw new PreconditionException(ErrorCode.MembershipPlanSourceSeasonEmpty, "The selected season has no plans to copy.");

            if (await context.MembershipPlans.AnyAsync(p => p.ClubId == request.ClubId && p.SeasonId == targetSeason.Id, cancellationToken))
                throw new PreconditionException(ErrorCode.MembershipPlanTargetSeasonNotEmpty, "The target season already has plans.");

            var yearOffset = targetSeason.Period.From.Year - sourceSeason.Period.From.Year;
            var sortOrder = 0;
            foreach (var plan in sourcePlans)
                context.MembershipPlans.Add(new MembershipPlan(request.ClubId, targetSeason.Id, plan.CopyData(yearOffset), ++sortOrder));

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
