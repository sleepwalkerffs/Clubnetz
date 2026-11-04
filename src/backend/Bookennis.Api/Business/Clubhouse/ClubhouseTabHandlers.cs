using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Clubhouse.SumUp;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Clubhouse;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using ClubhousePaymentMethod = Bookennis.Domain.Clubhouse.ClubhousePaymentMethod;
using ClubhouseTabStatus = Bookennis.Domain.Clubhouse.ClubhouseTabStatus;

namespace Bookennis.Api.Business.Clubhouse;

/// <summary>
/// The start screen of the host, built for speed: the open tabs (one tap books the next round), the members who play today (they come
/// to the bar after the match, closest to <paramref name="Now"/> first) and the regulars (most tabs in the last weeks). Every member is
/// listed once: an open tab wins over playing today, which wins over being a regular. Names and pictures only.
/// </summary>
public record GetClubhouseBoard(int ClubId, DateTimeOffset Now) : IQuery<ClubhouseBoardResult>
{
    public const int MaxPlayingToday = 24;
    public const int MaxRegulars = 12;
    public const int RegularsDays = 60;

    public class Handler(AppDbContext context) : IRequestHandler<GetClubhouseBoard, ClubhouseBoardResult>
    {
        public async Task<ClubhouseBoardResult> Handle(GetClubhouseBoard request, CancellationToken cancellationToken)
        {
            var today = ClubhouseQueries.LocalDate(request.Now);
            var dayStart = ClubhouseQueries.LocalMidnight(today);
            var dayEnd = ClubhouseQueries.LocalMidnight(today.AddDays(1));

            var openTabs = await context.ClubhouseTabs.AsNoTracking()
                .Include(t => t.Lines)
                .Where(t => t.ClubId == request.ClubId && (t.Status == ClubhouseTabStatus.Open || t.Status == ClubhouseTabStatus.AwaitingPayment))
                .ToListAsync(cancellationToken);

            // Players of other clubs (ATP) play here too, LoadGuests leaves them out
            var players = await context.Bookings.AsNoTracking()
                .Where(b => b.ClubId == request.ClubId && b.Interval.From < dayEnd && b.Interval.To > dayStart)
                .SelectMany(b => b.Players.Select(p => new { p.MemberId, b.Interval.From, b.Interval.To }))
                .ToListAsync(cancellationToken);

            var since = request.Now.AddDays(-RegularsDays);
            var regularCounts = await context.ClubhouseTabs.AsNoTracking()
                .Where(t => t.ClubId == request.ClubId && t.MemberId != null && t.OpenedAt >= since)
                .GroupBy(t => t.MemberId!.Value)
                .Select(g => new { MemberId = g.Key, Count = g.Count(), Last = g.Max(t => t.OpenedAt) })
                .OrderByDescending(g => g.Count).ThenByDescending(g => g.Last)
                .Take(MaxRegulars * 3)
                .ToListAsync(cancellationToken);

            var paidToday = await context.ClubhouseTabs.AsNoTracking()
                .Where(t => t.ClubId == request.ClubId && t.Status == ClubhouseTabStatus.Paid && t.PaidAt >= dayStart && t.PaidAt < dayEnd)
                .Select(t => new { t.PaymentMethod, Total = t.Lines.Sum(l => l.UnitPriceCents * l.Quantity) })
                .ToListAsync(cancellationToken);

            var listed = openTabs.Where(t => t.MemberId is not null).Select(t => t.MemberId!.Value).ToHashSet();

            // Distance to now: 0 while on court, otherwise to the nearer end of the booking
            var playingIds = players
                .GroupBy(p => p.MemberId)
                .Select(g => new
                {
                    MemberId = g.Key,
                    Distance = g.Min(p => p.From <= request.Now && request.Now < p.To ? TimeSpan.Zero : (p.From > request.Now ? p.From - request.Now : request.Now - p.To)),
                })
                .Where(p => !listed.Contains(p.MemberId))
                .OrderBy(p => p.Distance)
                .Select(p => p.MemberId)
                .ToList();

            var guests = await context.LoadGuests(request.ClubId,
                openTabs.Where(t => t.MemberId is not null).Select(t => t.MemberId!.Value).Concat(playingIds).Concat(regularCounts.Select(r => r.MemberId)),
                cancellationToken);

            var playingToday = playingIds.Where(guests.ContainsKey).Take(MaxPlayingToday).Select(id => guests[id]).ToList();
            listed.UnionWith(playingToday.Select(g => g.MemberId));

            var regulars = regularCounts
                .Where(r => !listed.Contains(r.MemberId) && guests.ContainsKey(r.MemberId))
                .Take(MaxRegulars)
                .Select(r => guests[r.MemberId])
                .ToList();

            return new ClubhouseBoardResult
            {
                OpenTabs = openTabs
                    .Select(t =>
                    {
                        var guest = t.MemberId is { } memberId ? guests.GetValueOrDefault(memberId) : null;
                        return new ClubhouseOpenTabDto
                        {
                            Id = t.Id,
                            MemberId = t.MemberId,
                            Name = ClubhouseQueries.Name(t, guest),
                            IsWalkIn = t.GuestName is not null,
                            ProfilePictureUrl = guest?.ProfilePictureUrl,
                            TotalCents = t.TotalCents,
                            ItemCount = t.Lines.Sum(l => l.Quantity),
                            IsAwaitingPayment = t.Status == ClubhouseTabStatus.AwaitingPayment,
                            OpenedAt = t.OpenedAt,
                            LastActivityAt = t.Lines.Count == 0 ? t.OpenedAt : t.Lines.Max(l => l.AddedAt),
                        };
                    })
                    .OrderByDescending(t => t.LastActivityAt)
                    .ToList(),
                PlayingToday = playingToday,
                Regulars = regulars,
                Today = new ClubhouseDaySummaryDto
                {
                    PaidTabCount = paidToday.Count,
                    CashCents = paidToday.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Cash).Sum(t => t.Total),
                    CardCents = paidToday.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Card).Sum(t => t.Total),
                    OtherCents = paidToday.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Other).Sum(t => t.Total),
                    OnlineCents = paidToday.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Online).Sum(t => t.Total),
                    OpenCents = openTabs.Sum(t => t.TotalCents),
                },
            };
        }
    }
}

/// <summary>
/// Every member of the club with name and picture, for the host's search. It is filtered on the device, so typing gives an answer at
/// once even on the slow network of a clubhouse.
/// </summary>
public record GetClubhouseGuests(int ClubId) : IQuery<List<ClubhouseGuestDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubhouseGuests, List<ClubhouseGuestDto>>
    {
        public Task<List<ClubhouseGuestDto>> Handle(GetClubhouseGuests request, CancellationToken cancellationToken)
            => context.Guests(request.ClubId)
                .OrderBy(g => g.LastName).ThenBy(g => g.FirstName).ThenBy(g => g.MemberId)
                .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Opens a tab for a member of the club, or returns the one that is still open (a member has one unpaid tab; while the member pays
/// online it is locked), or opens a tab for a guest from outside with a name.
/// </summary>
public record OpenClubhouseTab(int ClubId, int UserId, int? MemberId, string? GuestName) : ICommand<ClubhouseTabDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<OpenClubhouseTab, ClubhouseTabDto>
    {
        public async Task<ClubhouseTabDto> Handle(OpenClubhouseTab request, CancellationToken cancellationToken)
        {
            if (request.MemberId is { } memberId)
            {
                if (!await context.ClubMembers.AnyAsync(m => m.Id == memberId && m.ClubId == request.ClubId, cancellationToken))
                    throw new EntityNotFoundException(typeof(ClubMember));

                if (await FindOpenTab(request.ClubId, memberId, cancellationToken) is { } existing)
                    return await existing.ToDto(context, cancellationToken);
            }

            // Support mode has no member
            var hostId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var tab = new ClubhouseTab(request.ClubId, request.MemberId, request.MemberId is null ? request.GuestName : null, hostId, DateTimeOffset.UtcNow);
            context.ClubhouseTabs.Add(tab);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } && request.MemberId is not null)
            {
                // Opened at the same moment on another device: book onto that one
                context.Entry(tab).State = EntityState.Detached;
                var existing = await FindOpenTab(request.ClubId, request.MemberId.Value, cancellationToken) ?? throw new EntityNotFoundException(typeof(ClubhouseTab));
                return await existing.ToDto(context, cancellationToken);
            }

            return await tab.ToDto(context, cancellationToken);
        }

        private Task<ClubhouseTab?> FindOpenTab(int clubId, int memberId, CancellationToken cancellationToken)
            => context.ClubhouseTabs.Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.ClubId == clubId && t.MemberId == memberId && (t.Status == ClubhouseTabStatus.Open || t.Status == ClubhouseTabStatus.AwaitingPayment), cancellationToken);
    }
}

public record GetClubhouseTab(int ClubId, int TabId) : IQuery<ClubhouseTabDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubhouseTab, ClubhouseTabDto>
    {
        public async Task<ClubhouseTabDto> Handle(GetClubhouseTab request, CancellationToken cancellationToken)
        {
            var tab = await context.GetClubhouseTab(request.ClubId, request.TabId, cancellationToken);
            return await tab.ToDto(context, cancellationToken);
        }
    }
}

/// <summary>Books a round onto a tab: every item at its current name and price. Items of another club are not found.</summary>
public record AddClubhouseTabItems(int ClubId, int UserId, int TabId, IReadOnlyList<ClubhouseTabItemModel> Items) : ICommand<ClubhouseTabDto>
{
    public const int MaxItemsPerRound = 50;

    public class Handler(AppDbContext context) : IRequestHandler<AddClubhouseTabItems, ClubhouseTabDto>
    {
        public async Task<ClubhouseTabDto> Handle(AddClubhouseTabItems request, CancellationToken cancellationToken)
        {
            if (request.Items.Count is 0 or > MaxItemsPerRound)
                throw new PreconditionException(ClubhouseErrorCode.ClubhouseQuantityInvalid, [ClubhouseTab.MaxQuantity.ToString()], "Nothing to book.");

            var tab = await context.GetLockedClubhouseTab(request.ClubId, request.TabId, cancellationToken);

            var itemIds = request.Items.Select(i => i.ItemId).Distinct().ToList();
            var items = await context.ClubhouseItems
                .Where(i => i.ClubId == request.ClubId && itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, cancellationToken);
            if (items.Count != itemIds.Count)
                throw new EntityNotFoundException(typeof(ClubhouseItem));

            var hostId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            // The same item tapped twice in a round is one entry
            foreach (var group in request.Items.GroupBy(i => i.ItemId))
                tab.Add(items[group.Key], group.Sum(i => i.Quantity), hostId, now);

            await context.SaveChangesAsync(cancellationToken);
            return await tab.ToDto(context, cancellationToken);
        }
    }
}

/// <summary>Takes an entry off an open tab (booked by mistake).</summary>
public record RemoveClubhouseTabLine(int ClubId, int TabId, int LineId) : ICommand<ClubhouseTabDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<RemoveClubhouseTabLine, ClubhouseTabDto>
    {
        public async Task<ClubhouseTabDto> Handle(RemoveClubhouseTabLine request, CancellationToken cancellationToken)
        {
            var tab = await context.GetLockedClubhouseTab(request.ClubId, request.TabId, cancellationToken);
            if (!tab.Remove(request.LineId))
                throw new EntityNotFoundException(typeof(ClubhouseTabLine));

            await context.SaveChangesAsync(cancellationToken);
            return await tab.ToDto(context, cancellationToken);
        }
    }
}

/// <summary>The guest paid the host (cash, on the host's card terminal or otherwise): the tab is closed.</summary>
public record PayClubhouseTab(int ClubId, int UserId, int TabId, ClubhousePaymentMethod PaymentMethod) : ICommand<ClubhouseTabDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<PayClubhouseTab, ClubhouseTabDto>
    {
        public async Task<ClubhouseTabDto> Handle(PayClubhouseTab request, CancellationToken cancellationToken)
        {
            // Online payments are booked by SumUp's answer only, never by the host
            if (!Enum.IsDefined(request.PaymentMethod) || request.PaymentMethod == ClubhousePaymentMethod.Online)
                throw new ArgumentOutOfRangeException(nameof(request), request.PaymentMethod, "Unknown payment method.");

            var tab = await context.GetLockedClubhouseTab(request.ClubId, request.TabId, cancellationToken);
            var hostId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            tab.MarkPaid(request.PaymentMethod, hostId, DateTimeOffset.UtcNow);

            await context.SaveChangesAsync(cancellationToken);
            return await tab.ToDto(context, cancellationToken);
        }
    }
}

/// <summary>Discards an open tab with nothing on it (opened for the wrong guest).</summary>
public record DiscardClubhouseTab(int ClubId, int TabId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DiscardClubhouseTab>
    {
        public async Task<Unit> Handle(DiscardClubhouseTab request, CancellationToken cancellationToken)
        {
            var tab = await context.GetLockedClubhouseTab(request.ClubId, request.TabId, cancellationToken);
            tab.EnsureCanBeDiscarded();

            context.ClubhouseTabs.Remove(tab);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>
/// The paid tabs for the host's accounts, newest first: by the day they were paid (<c>From</c>/<c>To</c>, local dates, both optional;
/// without <c>From</c> the last 31 days).
/// </summary>
public record GetClubhouseTabs(int ClubId, DateOnly? From, DateOnly? To, DateTimeOffset Now) : IQuery<GetClubhouseTabsResult>
{
    public const int MaxTabs = 1000;
    public const int DefaultDays = 31;

    public class Handler(AppDbContext context) : IRequestHandler<GetClubhouseTabs, GetClubhouseTabsResult>
    {
        public async Task<GetClubhouseTabsResult> Handle(GetClubhouseTabs request, CancellationToken cancellationToken)
        {
            var from = ClubhouseQueries.LocalMidnight(request.From ?? ClubhouseQueries.LocalDate(request.Now).AddDays(-DefaultDays));
            var query = context.ClubhouseTabs.AsNoTracking()
                .Include(t => t.Lines)
                .Where(t => t.ClubId == request.ClubId && t.Status == ClubhouseTabStatus.Paid && t.PaidAt >= from);

            if (request.To is { } to)
            {
                var end = ClubhouseQueries.LocalMidnight(to.AddDays(1));
                query = query.Where(t => t.PaidAt < end);
            }

            var tabs = await query
                .OrderByDescending(t => t.PaidAt).ThenByDescending(t => t.Id)
                .Take(MaxTabs)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return new GetClubhouseTabsResult
            {
                Tabs = await tabs.ToDtos(context, request.ClubId, cancellationToken),
                CashCents = tabs.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Cash).Sum(t => t.TotalCents),
                CardCents = tabs.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Card).Sum(t => t.TotalCents),
                OtherCents = tabs.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Other).Sum(t => t.TotalCents),
                OnlineCents = tabs.Where(t => t.PaymentMethod == ClubhousePaymentMethod.Online).Sum(t => t.TotalCents),
            };
        }
    }
}

/// <summary>
/// The tabs of the signed-in member and of the children of the families they are a parent of (a parent pays for the children):
/// the open ones and the last paid ones, and whether they can be paid in the app (the host connected a SumUp account).
/// </summary>
public record GetMyClubhouseTabs(int ClubId, int UserId) : IQuery<MyClubhouseTabsResult>
{
    public const int MaxRecentlyPaid = 10;

    public class Handler(AppDbContext context, ISumUpGateway gateway) : IRequestHandler<GetMyClubhouseTabs, MyClubhouseTabsResult>
    {
        public async Task<MyClubhouseTabsResult> Handle(GetMyClubhouseTabs request, CancellationToken cancellationToken)
        {
            var memberIds = (await ActiveMembership.OwnAndChildMemberIds(context, request.ClubId, request.UserId, cancellationToken)).ToList();
            if (memberIds.Count == 0)
                return new MyClubhouseTabsResult { Open = [], RecentlyPaid = [], CanPayOnline = false };

            var mine = context.ClubhouseTabs.AsNoTracking()
                .Include(t => t.Lines)
                .Where(t => t.ClubId == request.ClubId && t.MemberId != null && memberIds.Contains(t.MemberId.Value));

            var open = await mine.Where(t => t.Status == ClubhouseTabStatus.Open || t.Status == ClubhouseTabStatus.AwaitingPayment)
                .OrderByDescending(t => t.OpenedAt)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
            var paid = await mine.Where(t => t.Status == ClubhouseTabStatus.Paid)
                .OrderByDescending(t => t.PaidAt).ThenByDescending(t => t.Id)
                .Take(MaxRecentlyPaid)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var canPayOnline = open.Count > 0 && await context.CanPayClubhouseTabsOnline(gateway, request.ClubId, cancellationToken);

            return new MyClubhouseTabsResult
            {
                CanPayOnline = canPayOnline,
                Open = await open.ToDtos(context, request.ClubId, cancellationToken),
                RecentlyPaid = await paid.ToDtos(context, request.ClubId, cancellationToken),
            };
        }
    }
}
