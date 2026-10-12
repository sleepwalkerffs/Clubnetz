using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using SharedOrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;
using SharedPaymentMethod = Bookennis.Shared.Controller.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Business.Memberships;

public static class MembershipQueryExtensions
{
    /// <summary>The season of the club with this id, or without an id the running season, else the next one.</summary>
    public static async Task<Season?> FindMembershipSeason(this AppDbContext context, int clubId, int? seasonId, CancellationToken cancellationToken)
    {
        if (seasonId is not null)
            return await context.Seasons.SingleRequiredAsync(s => s.Id == seasonId && s.ClubId == clubId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await context.Seasons
            .Where(s => s.ClubId == clubId && s.Period.To >= today)
            .OrderBy(s => s.Period.From)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static Task<Season> GetMembershipSeason(this AppDbContext context, int clubId, int seasonId, CancellationToken cancellationToken)
        => context.Seasons.SingleRequiredAsync(s => s.Id == seasonId && s.ClubId == clubId, cancellationToken);

    public static Task<MembershipOrder> GetMembershipOrder(this AppDbContext context, int clubId, int orderId, CancellationToken cancellationToken)
        => context.MembershipOrders
            .Include(o => o.Members)
            .SingleRequiredAsync(o => o.Id == orderId && o.ClubId == clubId, cancellationToken);

    /// <summary>
    /// Orders of a season are created one after the other, so two requests can't both find that a member has no membership yet.
    /// The lock is held until the transaction of the request ends.
    /// </summary>
    public static Task LockMembershipOrders(this AppDbContext context, int clubId, int seasonId, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({clubId}, {seasonId})", cancellationToken);

    /// <summary>The orders as the members they cover see them (no note, nothing of Stripe).</summary>
    public static async Task<List<MembershipOrderDto>> ToDtos(this IReadOnlyCollection<MembershipOrder> orders, AppDbContext context, CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
            return [];

        var memberIds = orders.SelectMany(o => o.Members.Select(m => m.MemberId)).Concat(orders.Where(o => o.PayerMemberId is not null).Select(o => o.PayerMemberId!.Value)).Distinct().ToList();
        var names = await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where memberIds.Contains(member.Id)
            select new { member.Id, user.FirstName, user.LastName }
        ).ToDictionaryAsync(m => m.Id, cancellationToken);

        var planIds = orders.Select(o => o.MembershipPlanId).Distinct().ToList();
        var grantsEnrollment = await context.MembershipPlans
            .Where(p => planIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.GrantsSeasonEnrollment, cancellationToken);

        return orders
            .OrderByDescending(o => o.Id)
            .Select(order => new MembershipOrderDto
            {
                Id = order.Id,
                SeasonId = order.SeasonId,
                MembershipPlanId = order.MembershipPlanId,
                PlanName = order.PlanName,
                AmountCents = order.AmountCents,
                Currency = order.Currency,
                Status = (SharedOrderStatus)order.Status,
                PaymentMethod = (SharedPaymentMethod?)order.PaymentMethod,
                PayerMemberId = order.PayerMemberId,
                PayerName = order.PayerMemberId is { } payerId && names.TryGetValue(payerId, out var payer) ? $"{payer.FirstName} {payer.LastName}" : null,
                Members = order.Members
                    .Where(m => names.ContainsKey(m.MemberId))
                    .Select(m => new MembershipOrderMemberDto { MemberId = m.MemberId, FirstName = names[m.MemberId].FirstName, LastName = names[m.MemberId].LastName })
                    .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                    .ToList(),
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(order.Metadata.Created, DateTimeKind.Utc)),
                ApprovedAt = order.ApprovedAt,
                PaidAt = order.PaidAt,
                GrantsSeasonEnrollment = grantsEnrollment.GetValueOrDefault(order.MembershipPlanId, true),
                CanPay = order.IsPayable,
                CanCancel = order.CanBeCancelledByMember,
            })
            .ToList();
    }

    public static async Task<MembershipOrderDto> ToDto(this MembershipOrder order, AppDbContext context, CancellationToken cancellationToken)
        => (await new[] { order }.ToDtos(context, cancellationToken)).Single();

    /// <summary>Paid or with a debit on its way: the fee counts as received and the members are enrolled.</summary>
    public static bool IsSettledOrSettling(this MembershipOrderStatus status)
        => status is MembershipOrderStatus.Paid or MembershipOrderStatus.Processing;
}
