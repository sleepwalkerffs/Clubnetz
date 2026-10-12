using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Microsoft.EntityFrameworkCore;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;
using SharedOrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;
using SharedPaymentMethod = Bookennis.Shared.Controller.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Business.Memberships;

/// <summary>
/// The treasurer's list of a season: every club member with how they got into the season, and every order.
/// Treasurers have no access to the members list, so this carries names, plan and payment only - never email, birthday or address.
/// </summary>
public record GetMembershipPayments(int ClubId, int SeasonId, MembershipOrderStatus? Status) : IQuery<GetMembershipPaymentsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMembershipPayments, GetMembershipPaymentsResult>
    {
        public async Task<GetMembershipPaymentsResult> Handle(GetMembershipPayments request, CancellationToken cancellationToken)
        {
            var overview = await MembershipPaymentsOverview.Load(context, request.ClubId, request.SeasonId, cancellationToken);

            return new GetMembershipPaymentsResult
            {
                SeasonId = overview.Season.Id,
                Members = overview.Members,
                Orders = overview.Orders.Where(o => request.Status is null || o.Status == (SharedOrderStatus)request.Status).ToList(),
            };
        }
    }
}

/// <summary>Everything the payments list, its summary and its export show about a season.</summary>
public record MembershipPaymentsOverview(Season Season, List<MembershipPaymentMemberDto> Members, List<MembershipPaymentOrderDto> Orders)
{
    public static async Task<MembershipPaymentsOverview> Load(AppDbContext context, int clubId, int seasonId, CancellationToken cancellationToken)
    {
        var season = await context.GetMembershipSeason(clubId, seasonId, cancellationToken);
        var people = await MembershipPeople.Load(context, clubId, season, aroundMemberIds: null, cancellationToken);

        var orders = await context.MembershipOrders
            .AsNoTracking()
            .Include(o => o.Members)
            .Where(o => o.ClubId == clubId && o.SeasonId == season.Id)
            .OrderByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        var methodByOrder = orders.ToDictionary(o => o.Id, o => o.PaymentMethod);

        var members = people.All
            .Select(p => new MembershipPaymentMemberDto
            {
                MemberId = p.MemberId,
                FirstName = p.FirstName,
                LastName = p.LastName,
                FamilyId = p.FamilyId,
                IsChild = p.IsChild,
                Enrollment = !p.IsEnrolled ? MembershipEnrollmentSource.NotEnrolled
                    : p.EnrolledByOrderId is not { } orderId ? MembershipEnrollmentSource.ByHand
                    : methodByOrder.GetValueOrDefault(orderId) == MembershipPaymentMethod.Online ? MembershipEnrollmentSource.PaidOnline
                    : MembershipEnrollmentSource.MarkedAsPaid,
            })
            .ToList();

        var orderDtos = orders.ConvertAll(o => new MembershipPaymentOrderDto
        {
            Id = o.Id,
            MembershipPlanId = o.MembershipPlanId,
            PlanName = o.PlanName,
            AmountCents = o.AmountCents,
            Currency = o.Currency,
            Status = (SharedOrderStatus)o.Status,
            PaymentMethod = (SharedPaymentMethod?)o.PaymentMethod,
            PayerMemberId = o.PayerMemberId,
            MemberIds = o.Members.Select(m => m.MemberId).ToList(),
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(o.Metadata.Created, DateTimeKind.Utc)),
            PaidAt = o.PaidAt,
            Note = o.Note,
        });

        return new MembershipPaymentsOverview(season, members, orderDtos);
    }

    public GetMembershipPaymentsSummaryResult ToSummary()
    {
        var paidMemberIds = Orders
            .Where(o => o.Status is SharedOrderStatus.Paid or SharedOrderStatus.Processing)
            .SelectMany(o => o.MemberIds)
            .ToHashSet();

        return new GetMembershipPaymentsSummaryResult
        {
            ReceivedCents = Orders.Where(o => o.Status == SharedOrderStatus.Paid).Sum(o => (long)o.AmountCents),
            ProcessingCents = Orders.Where(o => o.Status == SharedOrderStatus.Processing).Sum(o => (long)o.AmountCents),
            OpenCents = Orders
                .Where(o => o.Status is SharedOrderStatus.AwaitingApproval or SharedOrderStatus.AwaitingPayment or SharedOrderStatus.Failed)
                .Sum(o => (long)o.AmountCents),
            Currency = MembershipPlan.DefaultCurrency,
            MembersPaid = Members.Count(m => paidMemberIds.Contains(m.MemberId)),
            MembersEnrolled = Members.Count(m => m.Enrollment != MembershipEnrollmentSource.NotEnrolled),
            MembersTotal = Members.Count,
            OrdersByStatus = Orders.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count()),
        };
    }
}

public record GetMembershipPaymentsSummary(int ClubId, int SeasonId) : IQuery<GetMembershipPaymentsSummaryResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMembershipPaymentsSummary, GetMembershipPaymentsSummaryResult>
    {
        public async Task<GetMembershipPaymentsSummaryResult> Handle(GetMembershipPaymentsSummary request, CancellationToken cancellationToken)
            => (await MembershipPaymentsOverview.Load(context, request.ClubId, request.SeasonId, cancellationToken)).ToSummary();
    }
}
