using Bookennis.Shared.Controller.Clubhouse;

namespace Bookennis.Client.Services.HttpClients.Clubhouse;

/// <summary><c>api/Clubs/{clubId}/Clubhouse</c>: the menu and the tabs of the clubhouse host, and the own tabs of a member.</summary>
public interface IClubhouseHttpClient
{
    public Task<HttpResult<List<ClubhouseItemDto>>> GetItems(CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseItemDto>> CreateItem(SaveClubhouseItemModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseItemDto>> UpdateItem(int itemId, SaveClubhouseItemModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteItem(int itemId, CancellationToken cancellationToken = default);

    public Task<HttpResult<ClubhouseBoardResult>> GetBoard(CancellationToken cancellationToken = default);
    public Task<HttpResult<List<ClubhouseGuestDto>>> GetGuests(CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseTabDto>> OpenTab(OpenClubhouseTabModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseTabDto>> GetTab(int tabId, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseTabDto>> AddItems(int tabId, AddClubhouseTabItemsModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseTabDto>> RemoveLine(int tabId, int lineId, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseTabDto>> PayTab(int tabId, PayClubhouseTabModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DiscardTab(int tabId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetClubhouseTabsResult>> GetPaidTabs(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    public Task<HttpResult<MyClubhouseTabsResult>> GetMyTabs(CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseRedirectResult>> StartTabPayment(int tabId, CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseTabPaymentDto>> GetTabPayment(int tabId, int paymentId, CancellationToken cancellationToken = default);

    public Task<HttpResult<ClubhouseSumUpAccountDto>> GetSumUpAccount(CancellationToken cancellationToken = default);
    public Task<HttpResult<ClubhouseRedirectResult>> ConnectSumUp(CancellationToken cancellationToken = default);
    public Task<HttpResult> DisconnectSumUp(CancellationToken cancellationToken = default);
}
