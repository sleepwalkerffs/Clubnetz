using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// What the member can do about the membership of a season: the plans with the reasons why some can't be chosen,
/// the people an order can cover (the member and, for a parent, the family) and the orders that exist.
/// Without a season id the running season is used, else the next one.
/// </summary>
public record GetMembershipOptions(int ClubId, int UserId, int? SeasonId) : IQuery<GetMembershipOptionsResult>
{
    public class Handler(AppDbContext context, IPaymentAvailability paymentAvailability) : IRequestHandler<GetMembershipOptions, GetMembershipOptionsResult>
    {
        public async Task<GetMembershipOptionsResult> Handle(GetMembershipOptions request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var season = await context.FindMembershipSeason(request.ClubId, request.SeasonId, cancellationToken);
            if (season is null)
            {
                return new GetMembershipOptionsResult
                {
                    SeasonId = null,
                    SeasonStart = null,
                    SeasonEnd = null,
                    OnlinePaymentAvailable = false,
                    People = [],
                    Plans = [],
                    Orders = [],
                };
            }

            var people = await MembershipPeople.Load(context, request.ClubId, season, [memberId], cancellationToken);
            var orderable = people.OrderableBy(memberId);
            var orderableIds = orderable.ConvertAll(p => p.MemberId);

            var plans = await context.MembershipPlans
                .AsNoTracking()
                .Where(p => p.ClubId == request.ClubId && p.SeasonId == season.Id && p.IsSelfService)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);

            var orders = await context.MembershipOrders
                .AsNoTracking()
                .Include(o => o.Members)
                .Where(o => o.ClubId == request.ClubId
                         && o.SeasonId == season.Id
                         && (orderableIds.Contains(o.PayerMemberId ?? 0) || o.Members.Any(m => orderableIds.Contains(m.MemberId))))
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return new GetMembershipOptionsResult
            {
                SeasonId = season.Id,
                SeasonStart = season.Period.From,
                SeasonEnd = season.Period.To,
                OnlinePaymentAvailable = await paymentAvailability.CanAcceptPayments(request.ClubId, cancellationToken),
                People = orderable.ConvertAll(p => new MembershipPersonDto
                {
                    MemberId = p.MemberId,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    IsMe = p.MemberId == memberId,
                    IsChild = p.IsChild,
                    IsEnrolled = p.IsEnrolled,
                    HasMembership = p.HasMembership,
                }),
                Plans = plans.ConvertAll(plan => new MembershipPlanOptionDto
                {
                    Plan = plan.ToDto(),
                    Choices = GetChoices(plan, people, orderable, today),
                }),
                Orders = await orders.ToDtos(context, cancellationToken),
            };
        }

        private static List<MembershipPlanChoiceDto> GetChoices(MembershipPlan plan, MembershipPeople people, List<MembershipPerson> orderable, DateOnly today)
        {
            // A plan for one member: one choice per person. A couple or family: everybody who has no membership yet, as far as the plan covers them.
            var choices = plan.IsGroupPlan
                ? [GroupMembers(plan, orderable)]
                : orderable.Select(p => new List<MembershipPerson> { p }).ToList();

            return choices
                .Where(members => members.Count > 0)
                .Select(members =>
                {
                    var reasons = people.Check(plan, today, members);
                    return new MembershipPlanChoiceDto
                    {
                        MemberIds = members.ConvertAll(m => m.MemberId),
                        IsEligible = reasons.Count == 0,
                        Reasons = reasons.Select(r => r.ToString()).ToList(),
                    };
                })
                .ToList();
        }

        private static List<MembershipPerson> GroupMembers(MembershipPlan plan, List<MembershipPerson> orderable)
        {
            var open = orderable.Where(p => !p.HasMembership).ToList();

            // The caller comes first in the list, so with one adult it is the caller
            var adults = open.Where(p => !p.IsChild).Take(plan.MaxAdults);
            var children = plan.IncludesChildren ? open.Where(p => p.IsChild) : [];
            var members = adults.Concat(children).ToList();

            // Everybody already has a membership: show the plan with that reason instead of hiding it
            return members.Count > 0 ? members : orderable.Take(1).ToList();
        }
    }
}
