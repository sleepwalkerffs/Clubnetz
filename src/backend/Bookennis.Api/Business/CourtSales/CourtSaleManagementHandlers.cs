using System.Globalization;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Data;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.CourtSales;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using BookingCreditReason = Bookennis.Domain.CourtSales.BookingCreditReason;
using CourtPurchasePaymentMethod = Bookennis.Domain.CourtSales.CourtPurchasePaymentMethod;
using CourtPurchaseStatus = Bookennis.Domain.CourtSales.CourtPurchaseStatus;
using SharedReason = Bookennis.Shared.Controller.CourtSales.BookingCreditReason;
using SharedStatus = Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>
/// The purchases of the club for the treasurers, newest slot first: by the time of the slot (<c>From</c>/<c>To</c>, local dates of the
/// club's time zone, both optional) and status. The buyer only with the name.
/// </summary>
public record GetCourtPurchases(int ClubId, DateOnly? From, DateOnly? To, SharedStatus? Status) : IQuery<GetCourtPurchasesResult>
{
    public const int MaxPurchases = 1000;

    public class Handler(AppDbContext context) : IRequestHandler<GetCourtPurchases, GetCourtPurchasesResult>
    {
        public async Task<GetCourtPurchasesResult> Handle(GetCourtPurchases request, CancellationToken cancellationToken)
        {
            var settings = await context.GetCourtSaleSettings(request.ClubId, cancellationToken);
            var timeZone = settings.GetTimeZone();

            var query = context.CourtPurchases.AsNoTracking().Where(p => p.ClubId == request.ClubId);

            if (request.From is { } from)
            {
                var start = LocalMidnight(from, timeZone);
                query = query.Where(p => p.Interval.From >= start);
            }

            if (request.To is { } to)
            {
                var end = LocalMidnight(to.AddDays(1), timeZone);
                query = query.Where(p => p.Interval.From < end);
            }

            if (request.Status is { } status)
            {
                var domainStatus = (CourtPurchaseStatus)status;
                query = query.Where(p => p.Status == domainStatus);
            }

            var purchases = await query
                .OrderByDescending(p => p.Interval.From)
                .ThenByDescending(p => p.Id)
                .Take(MaxPurchases)
                .ToListAsync(cancellationToken);

            var sold = purchases.Where(p => p.Status is CourtPurchaseStatus.Paid or CourtPurchaseStatus.Processing).ToList();
            return new GetCourtPurchasesResult
            {
                Purchases = await purchases.ToListItems(context, cancellationToken),
                Summary = new CourtPurchasesSummaryDto
                {
                    SoldCount = sold.Count,
                    OnlineAmountCents = sold.Where(p => p.PaymentMethod == CourtPurchasePaymentMethod.Online).Sum(p => p.AmountDueCents),
                    CreditsUsed = sold.Count(p => p.CreditUsed),
                    AwaitingPaymentCount = purchases.Count(p => p.Status == CourtPurchaseStatus.AwaitingPayment),
                    NeedsAttentionCount = purchases.Count(p => p.Status is CourtPurchaseStatus.Refunded or CourtPurchaseStatus.Disputed),
                },
            };
        }

        private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo timeZone)
        {
            var local = date.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
        }
    }
}

/// <summary>
/// A treasurer or admin cancels a purchase: its booking is deleted when it has not started yet and a credit it used comes back.
/// Money paid online is refunded in the Stripe dashboard (not by Clubnetz); the result says when that is needed.
/// </summary>
public record CancelCourtPurchase(int ClubId, int PurchaseId, int UserId) : ICommand<CancelCourtPurchaseResult>
{
    public class Handler(AppDbContext context, ICourtPurchaseSettlement settlement, IMembershipCheckout checkout) : IRequestHandler<CancelCourtPurchase, CancelCourtPurchaseResult>
    {
        public async Task<CancelCourtPurchaseResult> Handle(CancelCourtPurchase request, CancellationToken cancellationToken)
        {
            var purchase = await context.GetCourtPurchase(request.ClubId, request.PurchaseId, cancellationToken);
            var openSession = purchase.Status == CourtPurchaseStatus.AwaitingPayment ? purchase.StripeCheckoutSessionId : null;

            // Support mode has no member
            var cancelledBy = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            purchase.Cancel(cancelledBy, DateTimeOffset.UtcNow);
            await settlement.RemoveBooking(purchase, onlyUpcoming: true, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // The payment page can't take money any more; a payment that arrives anyway is reported to the treasurers
            if (openSession is not null)
                await checkout.ExpireSession(purchase.ClubId, openSession, cancellationToken);

            var item = (await new[] { purchase }.ToListItems(context, cancellationToken)).Single();
            return new CancelCourtPurchaseResult { Purchase = item, RefundInStripeDashboard = purchase.HasOnlinePayment };
        }
    }
}

/// <summary>The booking credits of every member who ever got one, with the grants. Names only.</summary>
public record GetBookingCredits(int ClubId) : IQuery<GetBookingCreditsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetBookingCredits, GetBookingCreditsResult>
    {
        public async Task<GetBookingCreditsResult> Handle(GetBookingCredits request, CancellationToken cancellationToken)
        {
            var grants = await context.BookingCreditGrants.AsNoTracking()
                .Where(g => g.ClubId == request.ClubId)
                .OrderByDescending(g => g.Id)
                .ToListAsync(cancellationToken);

            var memberIds = grants.Select(g => g.MemberId).Distinct().ToList();
            return new GetBookingCreditsResult { Members = await BookingCreditBalances.Load(context, request.ClubId, memberIds, grants, cancellationToken) };
        }
    }
}

/// <summary>
/// Gives a member credits (positive) or takes some back (negative). The member must belong to the club; the balance can't go below 0.
/// </summary>
public record GrantBookingCredits(int ClubId, int UserId, int MemberId, int Count, string? Note) : ICommand<BookingCreditBalanceDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GrantBookingCredits, BookingCreditBalanceDto>
    {
        public async Task<BookingCreditBalanceDto> Handle(GrantBookingCredits request, CancellationToken cancellationToken)
        {
            if (!await context.ClubMembers.AnyAsync(m => m.Id == request.MemberId && m.ClubId == request.ClubId, cancellationToken))
                throw new EntityNotFoundException(typeof(ClubMember));

            var grantedBy = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var grant = new BookingCreditGrant(request.ClubId, request.MemberId, request.Count, BookingCreditReason.GrantedByClub, request.Note, grantedBy);

            // Grants and purchases of the club one after the other, so the balance can't be taken below 0 twice at the same time
            await context.LockCourts(request.ClubId, cancellationToken);
            var balance = await context.GetCreditBalance(request.ClubId, request.MemberId, cancellationToken);
            if (balance + request.Count < 0)
                throw new PreconditionException(CourtPurchaseErrorCode.BookingCreditBalanceTooLow, [balance.ToString(CultureInfo.InvariantCulture)], "The balance can't go below 0.");

            context.BookingCreditGrants.Add(grant);
            await context.SaveChangesAsync(cancellationToken);

            var grants = await context.BookingCreditGrants.AsNoTracking()
                .Where(g => g.ClubId == request.ClubId && g.MemberId == request.MemberId)
                .OrderByDescending(g => g.Id)
                .ToListAsync(cancellationToken);

            return (await BookingCreditBalances.Load(context, request.ClubId, [request.MemberId], grants, cancellationToken)).Single();
        }
    }
}

/// <summary>Members of the club a treasurer can give credits to, by name (first or last name starts with every term). Names only.</summary>
public record GetBookingCreditCandidates(int ClubId, string? Search) : IQuery<List<BookingCreditCandidateDto>>
{
    public const int MaxResults = 50;

    public class Handler(AppDbContext context) : IRequestHandler<GetBookingCreditCandidates, List<BookingCreditCandidateDto>>
    {
        public async Task<List<BookingCreditCandidateDto>> Handle(GetBookingCreditCandidates request, CancellationToken cancellationToken)
        {
            var query =
                from member in context.ClubMembers
                join user in context.Users on member.UserId equals user.Id
                where member.ClubId == request.ClubId
                select new { member.Id, user.FirstName, user.LastName, UserId = user.Id };

            foreach (var term in (request.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
            {
                var pattern = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(m => EF.Functions.ILike(m.FirstName, pattern) || EF.Functions.ILike(m.LastName, pattern));
            }

            var members = await query
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .Take(MaxResults)
                .Select(m => new BookingCreditCandidateDto
                {
                    MemberId = m.Id,
                    FirstName = m.FirstName,
                    LastName = m.LastName,
                    ProfilePictureUrl = context.UserProfilePictures.Any(p => p.UserId == m.UserId) ? "/api/Profile/picture/" + m.UserId : null,
                })
                .ToListAsync(cancellationToken);

            return members;
        }
    }
}

internal static class BookingCreditBalances
{
    public static async Task<List<BookingCreditBalanceDto>> Load(AppDbContext context, int clubId, List<int> memberIds, List<BookingCreditGrant> grants, CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
            return [];

        var names = await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == clubId && memberIds.Contains(member.Id)
            select new { member.Id, user.FirstName, user.LastName }
        ).ToDictionaryAsync(m => m.Id, cancellationToken);

        var used = await context.CourtPurchases
            .Where(p => p.ClubId == clubId && p.MemberId != null && memberIds.Contains(p.MemberId.Value) && p.CreditUsed && CourtPurchase.CreditHoldingStatuses.Contains(p.Status))
            .GroupBy(p => p.MemberId!.Value)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, cancellationToken);

        var grantedByIds = grants.Where(g => g.GrantedByMemberId is not null).Select(g => g.GrantedByMemberId!.Value).Distinct().ToList();
        var grantedBy = await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == clubId && grantedByIds.Contains(member.Id)
            select new { member.Id, Name = user.FirstName + " " + user.LastName }
        ).ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        return memberIds
            .Where(names.ContainsKey)
            .Select(memberId =>
            {
                var own = grants.Where(g => g.MemberId == memberId).ToList();
                var granted = own.Sum(g => g.Count);
                var usedCount = used.GetValueOrDefault(memberId);
                return new BookingCreditBalanceDto
                {
                    MemberId = memberId,
                    FirstName = names[memberId].FirstName,
                    LastName = names[memberId].LastName,
                    Granted = granted,
                    Used = usedCount,
                    Balance = granted - usedCount,
                    Grants = own.Select(g => new BookingCreditGrantDto
                    {
                        Id = g.Id,
                        Count = g.Count,
                        Reason = (SharedReason)g.Reason,
                        Note = g.Note,
                        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(g.Metadata.Created, DateTimeKind.Utc)),
                        GrantedByName = g.GrantedByMemberId is { } id ? grantedBy.GetValueOrDefault(id) : null,
                    }).ToList(),
                };
            })
            .OrderBy(b => b.LastName).ThenBy(b => b.FirstName)
            .ToList();
    }
}
