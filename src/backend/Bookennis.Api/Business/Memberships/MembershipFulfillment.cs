using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

public interface IMembershipFulfillment
{
    /// <summary>
    /// Gives the members of an order what they paid for: they are enrolled in the season, unless the plan does not enroll (passive members).
    /// Call it when an order becomes paid (or its debit was started) and save together with the status change.
    /// Members who are already enrolled are left as they are, so calling it twice does no harm.
    /// </summary>
    Task Complete(MembershipOrder order, CancellationToken cancellationToken);
}

/// <summary>
/// The one place where an order turns into season enrollments, for every way an order gets paid: online payment,
/// "mark as paid" and confirming a free plan. The new <see cref="MemberSeason"/> raises the usual
/// <c>MemberActivatedForSeasonDomainEvent</c>, which sends the welcome / season email like enrolling by hand does.
/// </summary>
public class MembershipFulfillment(AppDbContext context) : IMembershipFulfillment
{
    public async Task Complete(MembershipOrder order, CancellationToken cancellationToken)
    {
        var grantsEnrollment = await context.MembershipPlans
            .Where(p => p.Id == order.MembershipPlanId && p.ClubId == order.ClubId)
            .Select(p => p.GrantsSeasonEnrollment)
            .SingleAsync(cancellationToken);

        if (!grantsEnrollment)
            return;

        var memberIds = order.Members.Select(m => m.MemberId).ToList();
        var alreadyEnrolled = await context.MemberSeasons
            .Where(ms => ms.SeasonId == order.SeasonId && memberIds.Contains(ms.MemberId))
            .Select(ms => ms.MemberId)
            .ToListAsync(cancellationToken);

        // Enrollments added in this unit of work that are not saved yet
        var pending = context.MemberSeasons.Local.Where(ms => ms.SeasonId == order.SeasonId).Select(ms => ms.MemberId).ToList();

        foreach (var memberId in memberIds.Except(alreadyEnrolled).Except(pending))
            context.MemberSeasons.Add(new MemberSeason(memberId, order.SeasonId, order.Id));
    }
}
