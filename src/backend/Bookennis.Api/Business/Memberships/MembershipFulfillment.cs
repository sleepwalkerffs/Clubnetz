using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
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

    /// <summary>
    /// Takes back what <see cref="Complete"/> gave: removes the season enrollments this order created, e.g. when its SEPA debit failed.
    /// Members enrolled by hand or by another order keep their enrollment. Save together with the status change.
    /// </summary>
    Task Revoke(MembershipOrder order, CancellationToken cancellationToken);
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
        var plan = await context.MembershipPlans
            .Where(p => p.Id == order.MembershipPlanId && p.ClubId == order.ClubId)
            .Select(p => new { p.GrantsSeasonEnrollment, p.Sports })
            .SingleAsync(cancellationToken);

        if (!plan.GrantsSeasonEnrollment)
            return;

        var memberIds = order.Members.Select(m => m.MemberId).ToList();

        // Saved enrollments and the ones added in this unit of work that are not saved yet
        var enrollments = await context.MemberSeasons
            .Where(ms => ms.SeasonId == order.SeasonId && memberIds.Contains(ms.MemberId))
            .ToListAsync(cancellationToken);
        enrollments.AddRange(context.MemberSeasons.Local.Where(ms => ms.SeasonId == order.SeasonId && memberIds.Contains(ms.MemberId) && !enrollments.Contains(ms)));

        foreach (var memberId in memberIds)
        {
            // One membership per sport: a member enrolled for tennis who pays for padel gets padel added
            var enrollment = enrollments.FirstOrDefault(ms => ms.MemberId == memberId);
            if (enrollment is null)
                context.MemberSeasons.Add(new MemberSeason(memberId, order.SeasonId, order.Id, plan.Sports));
            else
                enrollment.SetSports(Sports.Union(enrollment.Sports, plan.Sports));
        }
    }

    public async Task Revoke(MembershipOrder order, CancellationToken cancellationToken)
    {
        var planSports = await context.MembershipPlans
            .Where(p => p.Id == order.MembershipPlanId)
            .Select(p => p.Sports)
            .SingleAsync(cancellationToken);

        var memberIds = order.Members.Select(m => m.MemberId).ToList();

        // Bookings the members already made stay; they just can't book anything new in the season (for the sports of the order)
        var enrollments = await context.MemberSeasons
            .Where(ms => ms.SeasonId == order.SeasonId && (ms.MembershipOrderId == order.Id || memberIds.Contains(ms.MemberId)))
            .ToListAsync(cancellationToken);

        foreach (var enrollment in enrollments)
        {
            // Take back the sports of this order. What is left came from another order or from the treasurer (tennis paid, padel failed).
            var left = planSports.Length > 0 && enrollment.Sports.Length > 0 ? enrollment.Sports.Except(planSports).ToArray() : [];

            if (left.Length > 0)
                enrollment.SetSports(left);
            else if (enrollment.MembershipOrderId == order.Id)
                context.MemberSeasons.Remove(enrollment);

            // Otherwise the enrollment was made by hand or by another order for every sport: it stays as it is
        }
    }
}
