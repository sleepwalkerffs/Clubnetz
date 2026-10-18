using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Clubhouse;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Clubhouse;

namespace Bookennis.Client.Services.Store.Clubhouse;

public class ClubhouseStore(IClubhouseHttpClient httpClient) : SemaphoreStore, IClubhouseStore
{
    public event Action? OnClubhouseChanged;

    public List<ClubhouseItemDto> Items { get; private set; } = [];
    public ClubhouseBoardResult? Board { get; private set; }
    public List<ClubhouseGuestDto>? Guests { get; private set; }
    public ClubhouseTabDto? Tab { get; private set; }
    public GetClubhouseTabsResult? PaidTabs { get; private set; }
    public MyClubhouseTabsResult? MyTabs { get; private set; }

    public Task LoadItems() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetItems(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Items = response.Dto;
            OnClubhouseChanged?.Invoke();
        }
    }, nameof(LoadItems));

    public Task LoadBoard() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetBoard(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Board = response.Dto;
            OnClubhouseChanged?.Invoke();
        }
    }, nameof(LoadBoard));

    public Task LoadGuests() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetGuests(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Guests = response.Dto;
            OnClubhouseChanged?.Invoke();
        }
    }, nameof(LoadGuests));

    public Task LoadTab(int tabId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Tab?.Id != tabId)
            Tab = null;

        var response = await httpClient.GetTab(tabId, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Tab = response.Dto;
            OnClubhouseChanged?.Invoke();
        }
    }, nameof(LoadTab));

    public Task LoadPaidTabs(DateOnly? from, DateOnly? to) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetPaidTabs(from, to, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            PaidTabs = response.Dto;
            OnClubhouseChanged?.Invoke();
        }
    }, nameof(LoadPaidTabs));

    public Task LoadMyTabs() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetMyTabs(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            MyTabs = response.Dto;
            OnClubhouseChanged?.Invoke();
        }
    }, nameof(LoadMyTabs));

    public async Task<HttpResult<ClubhouseItemDto>> SaveItem(int? itemId, SaveClubhouseItemModel model)
        => (HttpResult<ClubhouseItemDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = itemId is { } id
                ? await httpClient.UpdateItem(id, model, cancellationToken)
                : await httpClient.CreateItem(model, cancellationToken);
            if (result.Success)
                await LoadItems();

            return result;
        }, nameof(SaveItem));

    public Task<HttpResult> DeleteItem(int itemId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeleteItem(itemId, cancellationToken);
            if (result.Success)
                await LoadItems();

            return result;
        }, nameof(DeleteItem));

    public async Task<HttpResult<ClubhouseTabDto>> OpenTab(int? memberId, string? guestName)
        => (HttpResult<ClubhouseTabDto>)await RunInSavingContextAsync(async cancellationToken =>
            SetTab(await httpClient.OpenTab(new OpenClubhouseTabModel { MemberId = memberId, GuestName = guestName }, cancellationToken)), nameof(OpenTab));

    public async Task<HttpResult<ClubhouseTabDto>> AddItems(int tabId, IReadOnlyDictionary<int, int> quantitiesByItemId)
        => (HttpResult<ClubhouseTabDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var model = new AddClubhouseTabItemsModel { Items = quantitiesByItemId.Select(i => new ClubhouseTabItemModel { ItemId = i.Key, Quantity = i.Value }).ToList() };
            return SetTab(await httpClient.AddItems(tabId, model, cancellationToken));
        }, nameof(AddItems));

    public async Task<HttpResult<ClubhouseTabDto>> RemoveLine(int tabId, int lineId)
        => (HttpResult<ClubhouseTabDto>)await RunInSavingContextAsync(async cancellationToken =>
            SetTab(await httpClient.RemoveLine(tabId, lineId, cancellationToken)), nameof(RemoveLine));

    public async Task<HttpResult<ClubhouseTabDto>> PayTab(int tabId, ClubhousePaymentMethod paymentMethod)
        => (HttpResult<ClubhouseTabDto>)await RunInSavingContextAsync(async cancellationToken =>
            SetTab(await httpClient.PayTab(tabId, new PayClubhouseTabModel { PaymentMethod = paymentMethod }, cancellationToken)), nameof(PayTab));

    public Task<HttpResult> DiscardTab(int tabId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DiscardTab(tabId, cancellationToken);
            if (result.Success && Tab?.Id == tabId)
            {
                Tab = null;
                OnClubhouseChanged?.Invoke();
            }

            return result;
        }, nameof(DiscardTab));

    private HttpResult<ClubhouseTabDto> SetTab(HttpResult<ClubhouseTabDto> result)
    {
        if (result is { Success: true, Dto: not null })
        {
            Tab = result.Dto;
            OnClubhouseChanged?.Invoke();
        }

        return result;
    }
}
