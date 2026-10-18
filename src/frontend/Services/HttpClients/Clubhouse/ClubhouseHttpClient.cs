using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Clubhouse;

namespace Bookennis.Client.Services.HttpClients.Clubhouse;

public class ClubhouseHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IClubhouseHttpClient
{
    public async Task<HttpResult<List<ClubhouseItemDto>>> GetItems(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("items", cancellationToken)).AsHttpResult<List<ClubhouseItemDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseItemDto>> CreateItem(SaveClubhouseItemModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("items", model, jsonOptions, cancellationToken)).AsHttpResult<ClubhouseItemDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseItemDto>> UpdateItem(int itemId, SaveClubhouseItemModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"items/{itemId}", model, jsonOptions, cancellationToken)).AsHttpResult<ClubhouseItemDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteItem(int itemId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"items/{itemId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseBoardResult>> GetBoard(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("board", cancellationToken)).AsHttpResult<ClubhouseBoardResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<ClubhouseGuestDto>>> GetGuests(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("guests", cancellationToken)).AsHttpResult<List<ClubhouseGuestDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseTabDto>> OpenTab(OpenClubhouseTabModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("tabs", model, jsonOptions, cancellationToken)).AsHttpResult<ClubhouseTabDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseTabDto>> GetTab(int tabId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"tabs/{tabId}", cancellationToken)).AsHttpResult<ClubhouseTabDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseTabDto>> AddItems(int tabId, AddClubhouseTabItemsModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"tabs/{tabId}/items", model, jsonOptions, cancellationToken)).AsHttpResult<ClubhouseTabDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseTabDto>> RemoveLine(int tabId, int lineId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"tabs/{tabId}/lines/{lineId}", cancellationToken)).AsHttpResult<ClubhouseTabDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubhouseTabDto>> PayTab(int tabId, PayClubhouseTabModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"tabs/{tabId}/pay", model, jsonOptions, cancellationToken)).AsHttpResult<ClubhouseTabDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DiscardTab(int tabId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"tabs/{tabId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetClubhouseTabsResult>> GetPaidTabs(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from is { } f)
            query.Add($"from={f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is { } t)
            query.Add($"to={t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

        var url = query.Count == 0 ? "tabs" : "tabs?" + string.Join("&", query);
        return await (await httpClient.GetAsync(url, cancellationToken)).AsHttpResult<GetClubhouseTabsResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<MyClubhouseTabsResult>> GetMyTabs(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("my-tabs", cancellationToken)).AsHttpResult<MyClubhouseTabsResult>(jsonOptions, cancellationToken);
}
