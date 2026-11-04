using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Clubhouse;

namespace Bookennis.Client.Services.Store.Clubhouse;

/// <summary>
/// The clubhouse: the host books what guests eat and drink onto their tabs (clubhouse pages), members see their own tabs (My Club).
/// </summary>
public interface IClubhouseStore : ISemaphoreStore
{
    /// <summary>Raised whenever the menu, the board, a tab or the history was (re)loaded or changed.</summary>
    event Action? OnClubhouseChanged;

    /// <summary>The menu in menu order, with the hidden items.</summary>
    List<ClubhouseItemDto> Items { get; }

    /// <summary>The start screen of the host; <c>null</c> until it was loaded.</summary>
    ClubhouseBoardResult? Board { get; }

    /// <summary>All members of the club (names and pictures) for the host's search; <c>null</c> until loaded.</summary>
    List<ClubhouseGuestDto>? Guests { get; }

    /// <summary>The tab the host has open; <c>null</c> until it was loaded.</summary>
    ClubhouseTabDto? Tab { get; }

    /// <summary>The paid tabs of the chosen period; <c>null</c> until loaded.</summary>
    GetClubhouseTabsResult? PaidTabs { get; }

    /// <summary>The tabs of the signed-in member and of their children; <c>null</c> until loaded.</summary>
    MyClubhouseTabsResult? MyTabs { get; }

    /// <summary>The host's SumUp account (status only); <c>null</c> until loaded.</summary>
    ClubhouseSumUpAccountDto? SumUpAccount { get; }

    Task LoadItems();
    Task LoadBoard();

    /// <summary>Loads the guests once; the search filters them on the device.</summary>
    Task LoadGuests();

    Task LoadTab(int tabId);
    Task LoadPaidTabs(DateOnly? from, DateOnly? to);
    Task LoadMyTabs();
    Task LoadSumUpAccount();

    Task<HttpResult<ClubhouseItemDto>> SaveItem(int? itemId, SaveClubhouseItemModel model);
    Task<HttpResult> DeleteItem(int itemId);

    /// <summary>Opens a tab for a member (or returns the open one) or for a guest from outside.</summary>
    Task<HttpResult<ClubhouseTabDto>> OpenTab(int? memberId, string? guestName);

    Task<HttpResult<ClubhouseTabDto>> AddItems(int tabId, IReadOnlyDictionary<int, int> quantitiesByItemId);
    Task<HttpResult<ClubhouseTabDto>> RemoveLine(int tabId, int lineId);
    Task<HttpResult<ClubhouseTabDto>> PayTab(int tabId, ClubhousePaymentMethod paymentMethod);
    Task<HttpResult> DiscardTab(int tabId);

    /// <summary>Locks the tab and returns SumUp's payment page; leave for it with a full page load.</summary>
    Task<HttpResult<ClubhouseRedirectResult>> StartTabPayment(int tabId);

    /// <summary>The payment the member comes back from; the server asks SumUp first. Reloads the own tabs when it was paid.</summary>
    Task<HttpResult<ClubhouseTabPaymentDto>> CheckTabPayment(int tabId, int paymentId);

    /// <summary>SumUp's page where the host allows the payments; leave for it with a full page load.</summary>
    Task<HttpResult<ClubhouseRedirectResult>> ConnectSumUp();

    Task<HttpResult> DisconnectSumUp();
}
