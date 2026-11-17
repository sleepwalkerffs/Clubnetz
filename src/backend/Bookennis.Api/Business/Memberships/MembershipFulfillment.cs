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
        var grantsEnrollment = await context.MembershipPlans
            .Where(p => p.Id == order.MembershipPlanId && p.ClubId == order.ClubId)
            .Select(p => p.GrantsSeasonEnrollment)
            .SingleAsync(cancellationToken);

        if (!grantsEnrollment)
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
                context.MemberSeasons.Add(new MemberSeason(memberId, order.SeasonId, order.Id, order.Sports));
            else
                enrollment.SetSports(Sports.Union(enrollment.Sports, order.Sports));
        }
    }

    /// <summary>
    /// The enrollments an order made follow the orders of the member that are still paid or being paid: their sports are
    /// recomputed from those orders, and the enrollment goes when none is left. An enrollment made by hand is never touched
    /// (the treasurer decides), so an order that was never paid (a member withdrawing it) changes nothing either.
    /// </summary>
    public async Task Revoke(MembershipOrder order, CancellationToken cancellationToken)
    {
        var memberIds = order.Members.Select(m => m.MemberId).ToList();

        var enrollments = await context.MemberSeasons
            .Where(ms => ms.SeasonId == order.SeasonId && ms.MembershipOrderId != null && (ms.MembershipOrderId == order.Id || memberIds.Contains(ms.MemberId)))
            .ToListAsync(cancellationToken);
        var enrolledIds = enrollments.ConvertAll(ms => ms.MemberId);

        // The other orders of these members that enroll and are paid or being paid
        var others = await (
            from orderMember in context.MembershipOrderMembers
            join other in context.MembershipOrders on orderMember.MembershipOrderId equals other.Id
            join plan in context.MembershipPlans on other.MembershipPlanId equals plan.Id
            where other.ClubId == order.ClubId
               && other.SeasonId == order.SeasonId
               && other.Id != order.Id
               && (other.Status == MembershipOrderStatus.Paid || other.Status == MembershipOrderStatus.Processing)
               && plan.GrantsSeasonEnrollment
               && enrolledIds.Contains(orderMember.MemberId)
            orderby other.Id
            select new { orderMember.MemberId, OrderId = other.Id, other.Sports }
        ).ToListAsync(cancellationToken);

        // Bookings the members already made stay; they just can't book anything new in the season (for the sports they lost)
        foreach (var enrollment in enrollments)
        {
            var paid = others.Where(o => o.MemberId == enrollment.MemberId).ToList();
            if (paid.Count > 0)
            {
                enrollment.SetSports(paid.Skip(1).Aggregate(paid[0].Sports, (all, o) => Sports.Union(all, o.Sports)));
                if (enrollment.MembershipOrderId == order.Id)
                    enrollment.SetMembershipOrder(paid[0].OrderId);
            }
            else if (enrollment.MembershipOrderId == order.Id)
            {
                context.MemberSeasons.Remove(enrollment);
            }
            else if (order.Sports.Length > 0 && enrollment.Sports.Length > 0 && enrollment.Sports.Except(order.Sports).ToArray() is { Length: > 0 } left)
            {
                // Made by an order the treasurer refunded without revoking it: keep that, take back only what this order added
                enrollment.SetSports(left);
            }
        }
    }
}
