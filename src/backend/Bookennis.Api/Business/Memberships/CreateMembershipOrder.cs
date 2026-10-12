using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// A member chooses a plan for themself or, as a parent, for members of the family. The order then waits for the treasurer's
/// confirmation (plans that need one) or for its payment. The rules of the plan are checked here again, whatever the page showed.
/// </summary>
public record CreateMembershipOrder(int ClubId, int UserId, int MembershipPlanId, IReadOnlyCollection<int> MemberIds) : ICommand<MembershipOrderDto>
{
    public enum ErrorCode
    {
        /// <summary>The plan is assigned by the treasurer only.</summary>
        MembershipPlanNotSelfService = 0,
    }

    public class Handler(AppDbContext context, IMembershipNotifier notifier) : IRequestHandler<CreateMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(CreateMembershipOrder request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var plan = await context.MembershipPlans.SingleRequiredAsync(p => p.Id == request.MembershipPlanId && p.ClubId == request.ClubId, cancellationToken);
            if (!plan.IsSelfService)
                throw new PreconditionException(ErrorCode.MembershipPlanNotSelfService, "This plan is assigned by the club.");

            var season = await context.GetMembershipSeason(request.ClubId, plan.SeasonId, cancellationToken);
            await context.LockMembershipOrders(request.ClubId, season.Id, cancellationToken);

            var people = await MembershipPeople.Load(context, request.ClubId, season, [memberId], cancellationToken);
            var orderable = people.OrderableBy(memberId);

            // Only for oneself or, as a parent, for the own family. Everything else is an attempt to act for somebody else.
            var memberIds = request.MemberIds.Distinct().ToList();
            var members = orderable.Where(p => memberIds.Contains(p.MemberId)).ToList();
            if (members.Count != memberIds.Count)
                throw new AccessDeniedException("A membership can only be ordered for oneself or for the own family.");

            var order = new MembershipOrder(plan, memberId, memberIds);

            var reasons = people.Check(plan, DateOnly.FromDateTime(DateTime.UtcNow), members);
            if (reasons.Count > 0)
                throw new PreconditionException(reasons[0], $"The plan can't be chosen: {string.Join(", ", reasons)}.");

            context.MembershipOrders.Add(order);
            await context.SaveChangesAsync(cancellationToken);

            if (order.Status == MembershipOrderStatus.AwaitingApproval)
                await notifier.NeedsAttention(order, MembershipAttentionReason.ApprovalRequested, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }
    }
}
