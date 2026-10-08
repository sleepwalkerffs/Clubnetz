using Bookennis.Api.Data;
using Bookennis.Shared.Controller.SubscriptionPlans;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.SubscriptionPlans;

public record GetSubscriptionPlans(int ClubId, int UserId) : IQuery<GetSubscriptionPlansResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetSubscriptionPlans, GetSubscriptionPlansResult>
    {
        public async Task<GetSubscriptionPlansResult> Handle(GetSubscriptionPlans request, CancellationToken cancellationToken)
        {
            var plans = await context.SubscriptionPlans
                .Where(p => p.ClubId == request.ClubId && p.OwnerUserId == request.UserId)
                .OrderByDescending(p => p.StartDate)
                .ThenBy(p => p.Name)
                .Select(p => new SubscriptionPlanSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PlayersPerWeek = p.PlayersPerWeek,
                    ParticipantCount = p.Participants.Count,
                    HasSchedule = p.Assignments.Any(),
                })
                .ToListAsync(cancellationToken);

            return new GetSubscriptionPlansResult { Plans = plans };
        }
    }
}
