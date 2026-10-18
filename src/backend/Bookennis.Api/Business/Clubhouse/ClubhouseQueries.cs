using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubhouse;
using Bookennis.Shared.Controller.Clubhouse;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SharedCategory = Bookennis.Shared.Controller.Clubhouse.ClubhouseItemCategory;
using SharedPaymentMethod = Bookennis.Shared.Controller.Clubhouse.ClubhousePaymentMethod;
using SharedStatus = Bookennis.Shared.Controller.Clubhouse.ClubhouseTabStatus;

namespace Bookennis.Api.Business.Clubhouse;

public static class ClubhouseQueries
{
    /// <summary>
    /// The days of the host ("today", the history) are local days of this time zone, the one of the club calendar (all supported
    /// countries share it).
    /// </summary>
    public static TimeZoneInfo TimeZone => ClubEventInfo.TimeZone;

    public static Task<ClubhouseTab> GetClubhouseTab(this AppDbContext context, int clubId, int tabId, CancellationToken cancellationToken)
        => context.ClubhouseTabs.Include(t => t.Lines).SingleRequiredAsync(t => t.Id == tabId && t.ClubId == clubId, cancellationToken);

    /// <summary>Midnight (UTC) at the start of a local day of <see cref="TimeZone"/>.</summary>
    public static DateTimeOffset LocalMidnight(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateOnly LocalDate(DateTimeOffset moment) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, TimeZone).DateTime);

    public static ClubhouseItemDto ToDto(this ClubhouseItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Category = (SharedCategory)item.Category,
        PriceCents = item.PriceCents,
        Currency = item.Currency,
        IsAvailable = item.IsAvailable,
        SortOrder = item.SortOrder,
    };

    /// <summary>The menu in the order the host sees it: by category, then their own order, then by name.</summary>
    public static IOrderedQueryable<ClubhouseItem> InMenuOrder(this IQueryable<ClubhouseItem> items)
        => items.OrderBy(i => i.Category).ThenBy(i => i.SortOrder).ThenBy(i => i.Name).ThenBy(i => i.Id);

    /// <summary>Names and pictures of members of the club; members of other clubs are left out.</summary>
    public static async Task<Dictionary<int, ClubhouseGuestDto>> LoadGuests(this AppDbContext context, int clubId, IEnumerable<int> memberIds, CancellationToken cancellationToken)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await context.Guests(clubId)
            .Where(g => ids.Contains(g.MemberId))
            .ToDictionaryAsync(g => g.MemberId, cancellationToken);
    }

    /// <summary>The members of the club as guests of the clubhouse: name and picture, nothing more.</summary>
    public static IQueryable<ClubhouseGuestDto> Guests(this AppDbContext context, int clubId)
        => from member in context.ClubMembers
           join user in context.Users on member.UserId equals user.Id
           where member.ClubId == clubId
           select new ClubhouseGuestDto
           {
               MemberId = member.Id,
               FirstName = user.FirstName,
               LastName = user.LastName,
               ProfilePictureUrl = context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null,
           };

    public static async Task<List<ClubhouseTabDto>> ToDtos(this IReadOnlyCollection<ClubhouseTab> tabs, AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        var guests = await context.LoadGuests(clubId, tabs.Where(t => t.MemberId is not null).Select(t => t.MemberId!.Value), cancellationToken);
        return tabs.Select(t => t.ToDto(guests)).ToList();
    }

    public static async Task<ClubhouseTabDto> ToDto(this ClubhouseTab tab, AppDbContext context, CancellationToken cancellationToken)
        => (await new[] { tab }.ToDtos(context, tab.ClubId, cancellationToken)).Single();

    public static ClubhouseTabDto ToDto(this ClubhouseTab tab, IReadOnlyDictionary<int, ClubhouseGuestDto> guests)
    {
        var guest = tab.MemberId is { } memberId ? guests.GetValueOrDefault(memberId) : null;
        return new ClubhouseTabDto
        {
            Id = tab.Id,
            MemberId = tab.MemberId,
            Name = Name(tab, guest),
            IsWalkIn = tab.GuestName is not null,
            ProfilePictureUrl = guest?.ProfilePictureUrl,
            Status = (SharedStatus)tab.Status,
            Currency = tab.Currency,
            TotalCents = tab.TotalCents,
            OpenedAt = tab.OpenedAt,
            PaidAt = tab.PaidAt,
            PaymentMethod = (SharedPaymentMethod?)tab.PaymentMethod,
            Lines = tab.Lines
                .OrderByDescending(l => l.AddedAt).ThenByDescending(l => l.Id)
                .Select(l => new ClubhouseTabLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    Name = l.Name,
                    Category = (SharedCategory)l.Category,
                    UnitPriceCents = l.UnitPriceCents,
                    Quantity = l.Quantity,
                    TotalCents = l.TotalCents,
                    AddedAt = l.AddedAt,
                })
                .ToList(),
        };
    }

    /// <summary>The name of the member or of the guest from outside; empty once the member left the club (the client shows "former member").</summary>
    public static string Name(ClubhouseTab tab, ClubhouseGuestDto? guest)
        => tab.GuestName ?? (guest is null ? "" : $"{guest.FirstName} {guest.LastName}");
}
