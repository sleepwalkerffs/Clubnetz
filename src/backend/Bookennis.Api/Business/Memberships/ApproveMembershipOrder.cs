using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// The treasurer confirms that the member may have the plan (student, apprentice, ...). The member is told by email and can pay.
/// With <paramref name="MemberIds"/> the treasurer also sets who the order covers, e.g. the family that was put together for a family plan.
/// As in <see cref="RecordMembershipPayment"/> the treasurer decides, so the rules of the plan are not enforced: only that the members
/// belong to the club and have no other membership in the season.
/// </summary>
public record ApproveMembershipOrder(int ClubId, int MembershipOrderId, IReadOnlyCollection<int>? MemberIds = null) : ICommand<MembershipOrderDto>
{
    public class Handler(AppDbContext context, IMembershipNotifier notifier) : IRequestHandler<ApproveMembershipOrder, MembershipOrderDto>
    {
        public async Task<MembershipOrderDto> Handle(ApproveMembershipOrder request, CancellationToken cancellationToken)
        {
            var order = await context.GetMembershipOrder(request.ClubId, request.MembershipOrderId, cancellationToken);

            if (request.MemberIds is { } memberIds && !memberIds.ToHashSet().SetEquals(order.Members.Select(m => m.MemberId)))
                await SetMembers(order, memberIds.Distinct().ToList(), cancellationToken);

            order.Approve(DateTimeOffset.UtcNow);

            await context.SaveChangesAsync(cancellationToken);
            await notifier.Approved(order, cancellationToken);

            return await order.ToDto(context, cancellationToken);
        }

        private async Task SetMembers(MembershipOrder order, List<int> memberIds, CancellationToken cancellationToken)
        {
            var season = await context.GetMembershipSeason(order.ClubId, order.SeasonId, cancellationToken);
            await context.LockMembershipOrders(order.ClubId, season.Id, cancellationToken);

            var people = await MembershipPeople.Load(context, order.ClubId, season, memberIds, cancellationToken);
            if (memberIds.Any(id => people.Find(id) is null))
                throw new PreconditionException(RecordMembershipPayment.ErrorCode.MembershipMemberNotInClub, "A member is not a member of this club.");

            if (memberIds.Any(id => people.Find(id) is { HasMembership: true } person && person.ActiveOrderId != order.Id))
                throw new PreconditionException(MembershipEligibility.ErrorCode.AlreadyHasMembership, "A member already has a membership in this season.");

            order.SetMembers(memberIds);
        }
    }
}
