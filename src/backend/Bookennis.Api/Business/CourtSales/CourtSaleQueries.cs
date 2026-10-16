using Bookennis.Api.Data;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.CourtSales;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CourtPurchaseStatus = Bookennis.Domain.CourtSales.CourtPurchaseStatus;
using SharedPaymentMethod = Bookennis.Shared.Controller.CourtSales.CourtPurchasePaymentMethod;
using SharedStatus = Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>Errors of the court sale handlers. The names are the error codes the client localizes; never rename or renumber them.</summary>
public enum CourtPurchaseErrorCode
{
    /// <summary>The club does not sell court time (switched off or no play mode).</summary>
    CourtSalesNotEnabled = 0,

    /// <summary>The caller is enrolled in the active season and books for free.</summary>
    AlreadyEnrolled = 1,

    /// <summary>A minute of the slot has no price rule.</summary>
    CourtNotForSale = 2,

    /// <summary>The slot does not start on a full or half hour or is not a multiple of 30 minutes up to the maximum. Details: the maximum in minutes.</summary>
    CourtPurchaseDurationInvalid = 3,

    /// <summary>Details: the number of days a slot can be bought in advance.</summary>
    CourtPurchaseTooFarInAdvance = 4,

    /// <summary>The buyer already has the allowed number of bought bookings that are not over. Details: that number.</summary>
    CourtPurchaseLimitReached = 5,

    /// <summary>A bought booking can't be changed or deleted like other bookings: a treasurer cancels the purchase.</summary>
    BookingIsBought = 6,

    /// <summary>Taking back credits would take the balance below 0. Details: the balance.</summary>
    BookingCreditBalanceTooLow = 7,

    /// <summary>Only a member of the club can buy court time (an administrator in support mode has none).</summary>
    CourtPurchaseRequiresMember = 8,
}

public static class CourtSaleQueries
{
    /// <summary>The settings of the club, or the defaults (switched off) when it never saved any. Not tracked when new.</summary>
    public static async Task<CourtSaleSettings> GetCourtSaleSettings(this AppDbContext context, int clubId, CancellationToken cancellationToken)
        => await context.CourtSaleSettings.FirstOrDefaultAsync(s => s.ClubId == clubId, cancellationToken) ?? new CourtSaleSettings(clubId);

    /// <summary>The club sells court time: switched on with a play mode that still exists.</summary>
    public static bool IsSelling(this CourtSaleSettings settings) => settings.IsEnabled && settings.PlayModeId is not null;

    public static Task<List<CourtPriceRule>> GetCourtPriceRules(this AppDbContext context, int clubId, CancellationToken cancellationToken)
        => context.CourtPriceRules.Where(r => r.ClubId == clubId).OrderBy(r => r.Id).ToListAsync(cancellationToken);

    public static Task<CourtPurchase> GetCourtPurchase(this AppDbContext context, int clubId, int purchaseId, CancellationToken cancellationToken)
        => context.CourtPurchases.SingleRequiredAsync(p => p.Id == purchaseId && p.ClubId == clubId, cancellationToken);

    /// <summary>
    /// Everything that books a court of a club (bookings, batches, series, moves, purchases, a slot booked again after a late payment)
    /// runs one after the other, so two requests can't both find the same slot free (and a buyer can't pass the limit of upcoming
    /// bookings twice). Take it before the overlap check. The lock is held until the transaction of the command ends (all commands
    /// run in a transaction, <c>TransactionalRequestHandlerDecorator</c>); taking it twice in one transaction is fine.
    /// The one-key form of the advisory lock does not collide with the two-key form of <c>LockMembershipOrders</c>.
    /// </summary>
    public static Task LockCourts(this AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        var key = CourtLockNamespace + clubId;
        return context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    private const long CourtLockNamespace = 0x434F555254L << 32; // "COURT"

    /// <summary>Credits a member has left: the grants minus the purchases that hold a credit.</summary>
    public static async Task<int> GetCreditBalance(this AppDbContext context, int clubId, int memberId, CancellationToken cancellationToken)
    {
        var granted = await context.BookingCreditGrants
            .Where(g => g.ClubId == clubId && g.MemberId == memberId)
            .SumAsync(g => g.Count, cancellationToken);

        var used = await context.CourtPurchases
            .CountAsync(p => p.ClubId == clubId && p.MemberId == memberId && p.CreditUsed && CourtPurchase.CreditHoldingStatuses.Contains(p.Status), cancellationToken);

        return granted - used;
    }

    /// <summary>Bought bookings of the member that hold a slot and are not over yet.</summary>
    public static Task<int> CountUpcomingPurchases(this AppDbContext context, int clubId, int memberId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return context.CourtPurchases.CountAsync(
            p => p.ClubId == clubId && p.MemberId == memberId && CourtPurchase.ActiveStatuses.Contains(p.Status) && p.Interval.To > now,
            cancellationToken);
    }

    public static CourtPurchaseDto ToDto(this CourtPurchase purchase, string courtName) => new()
    {
        Id = purchase.Id,
        CourtId = purchase.CourtId,
        CourtName = courtName,
        BookingId = purchase.BookingId,
        Interval = purchase.Interval,
        PriceCents = purchase.PriceCents,
        CreditUsed = purchase.CreditUsed,
        AmountDueCents = purchase.AmountDueCents,
        Currency = purchase.Currency,
        Status = (SharedStatus)purchase.Status,
        PaymentMethod = (SharedPaymentMethod?)purchase.PaymentMethod,
        HoldExpiresAt = purchase.HoldExpiresAt,
        PaidAt = purchase.PaidAt,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(purchase.Metadata.Created, DateTimeKind.Utc)),
    };

    public static async Task<CourtPurchaseDto> ToDto(this CourtPurchase purchase, AppDbContext context, CancellationToken cancellationToken)
    {
        var courtName = await context.Courts.Where(c => c.Id == purchase.CourtId && c.ClubId == purchase.ClubId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);
        return purchase.ToDto(courtName ?? "");
    }

    /// <summary>The purchases as the treasurers see them: the name of the buyer, nothing else about the person.</summary>
    public static async Task<List<CourtPurchaseListItemDto>> ToListItems(this IReadOnlyCollection<CourtPurchase> purchases, AppDbContext context, CancellationToken cancellationToken)
    {
        if (purchases.Count == 0)
            return [];

        var clubId = purchases.First().ClubId;
        var memberIds = purchases.Where(p => p.MemberId is not null).Select(p => p.MemberId!.Value).Distinct().ToList();
        var names = await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == clubId && memberIds.Contains(member.Id)
            select new { member.Id, user.FirstName, user.LastName }
        ).ToDictionaryAsync(m => m.Id, cancellationToken);

        var courtIds = purchases.Select(p => p.CourtId).Distinct().ToList();
        var courts = await context.Courts.Where(c => c.ClubId == clubId && courtIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return purchases.Select(p =>
        {
            var name = p.MemberId is { } memberId && names.TryGetValue(memberId, out var n) ? n : null;
            return new CourtPurchaseListItemDto
            {
                Id = p.Id,
                MemberId = p.MemberId,
                FirstName = name?.FirstName ?? "",
                LastName = name?.LastName ?? "",
                CourtId = p.CourtId,
                CourtName = courts.GetValueOrDefault(p.CourtId, ""),
                BookingId = p.BookingId,
                Interval = p.Interval,
                PriceCents = p.PriceCents,
                CreditUsed = p.CreditUsed,
                AmountDueCents = p.AmountDueCents,
                Currency = p.Currency,
                Status = (SharedStatus)p.Status,
                PaymentMethod = (SharedPaymentMethod?)p.PaymentMethod,
                PaidAt = p.PaidAt,
                CancelledAt = p.CancelledAt,
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(p.Metadata.Created, DateTimeKind.Utc)),
                CanCancel = p.Status is CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Processing or CourtPurchaseStatus.Paid,
                HasOnlinePayment = p.HasOnlinePayment,
            };
        }).ToList();
    }

    public static CourtPriceRuleDto ToDto(this CourtPriceRule rule) => new()
    {
        Id = rule.Id,
        CourtIds = rule.CourtIds.ToList(),
        Weekdays = rule.Weekdays.ToList(),
        From = rule.From.ToTimeSpan(),
        To = rule.To == TimeOnly.MinValue ? TimeSpan.FromDays(1) : rule.To.ToTimeSpan(),
        PricePerHourCents = rule.PricePerHourCents,
        Currency = rule.Currency,
        Name = rule.Name,
    };

    /// <summary>The times of the client are <see cref="TimeSpan"/>s; 24:00 is midnight at the end of the day.</summary>
    public static CourtPriceRule.RuleData ToRuleData(this SaveCourtPriceRuleModel model)
        => new(model.CourtIds ?? [], model.Weekdays ?? [], ToTime(model.From), ToTime(model.To), model.PricePerHourCents, model.Name);

    private static TimeOnly ToTime(TimeSpan value)
    {
        if (value == TimeSpan.FromDays(1))
            return TimeOnly.MinValue;

        if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
            throw new PreconditionException(CourtSaleErrorCode.CourtPriceRuleInvalidTime, "Invalid time of day.");

        return TimeOnly.FromTimeSpan(value);
    }
}

public static class BoughtBookings
{
    /// <summary>
    /// A bought booking is not changed or deleted like other bookings: the buyer can't cancel after paying, and a treasurer cancels
    /// the purchase (<see cref="CancelCourtPurchase"/>), so the record of the payment stays right.
    /// </summary>
    public static async Task EnsureNotBought(this AppDbContext context, int bookingId, CancellationToken cancellationToken)
    {
        if (await context.CourtPurchases.AnyAsync(p => p.BookingId == bookingId, cancellationToken))
            throw new PreconditionException(CourtPurchaseErrorCode.BookingIsBought, "A bought booking is cancelled by a treasurer.");
    }
}
