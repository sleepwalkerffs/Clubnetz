using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// The treasurer records a fee that was paid outside of Clubnetz for members who have no order yet: creates a paid order and enrolls them.
/// The treasurer decides who gets which plan, so the rules of the plan (birth years, family, new members) are not enforced here.
/// Only one thing is: a member has one membership per season.
/// </summary>
public record RecordMembershipPayment(
    int ClubId,
    int UserId,
    int MembershipPlanId,
    IReadOnlyCollection<int> MemberIds,
    int? PayerMemberId,
    MembershipPaymentMethod Method,
    DateOnly PaidOn,
    string? Note) : ICommand<MembershipOrderDto>
{
    public enum ErrorCode
    {
        /// <summary>One of the members is not a member of this club.</summary>
        MembershipMemberNotInClub = 0,
    }

    public class Handler(AppDbContext context, IMembershipFulfillment fulfillment, IMembershipNotifier notifier) : IRequestHandler<RecordMembershipPayment, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(RecordMembershipPayment request, CancellationToken cancellationToken)
        {
            var plan = await context.MembershipPlans.SingleRequiredAsync(p => p.Id == request.MembershipPlanId && p.ClubId == request.ClubId, cancellationToken);
            var season = await context.GetMembershipSeason(request.ClubId, plan.SeasonId, cancellationToken);
            var treasurerMemberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            await context.LockMembershipOrders(request.ClubId, season.Id, cancellationToken);

            var memberIds = request.MemberIds.Distinct().ToList();
            var payerMemberId = request.PayerMemberId ?? memberIds.Select(id => (int?)id).FirstOrDefault();
            var involved = payerMemberId is null ? memberIds : memberIds.Append(payerMemberId.Value).Distinct().ToList();

            var people = await MembershipPeople.Load(context, request.ClubId, season, involved, cancellationToken);
            if (involved.Any(id => people.Find(id) is null))
                throw new PreconditionException(ErrorCode.MembershipMemberNotInClub, "A member is not a member of this club.");

            if (memberIds.Any(id => people.Find(id)!.HasMembership))
                throw new PreconditionException(MembershipEligibility.ErrorCode.AlreadyHasMembership, "A member already has a membership in this season.");

            var order = new MembershipOrder(plan, payerMemberId, memberIds, request.Note);
            if (order.IsFree)
                order.ConfirmFree(MembershipPayments.PaidAt(request.PaidOn));
            else
                order.MarkPaid(MembershipPayments.ManualMethod(request.Method), MembershipPayments.PaidAt(request.PaidOn), treasurerMemberId);

            // The order needs its id before the enrollments can point to it
            context.MembershipOrders.Add(order);
            await context.SaveChangesAsync(cancellationToken);

            await fulfillment.Complete(order, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (!order.IsFree)
                await notifier.PaymentReceived(order, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }
    }
}
