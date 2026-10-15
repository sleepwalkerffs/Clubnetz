using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Wrapped;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Wrapped;

namespace Bookennis.Client.Services.Store.Wrapped;

public class WrappedStore(IWrappedHttpClient httpClient) : SemaphoreStore, IWrappedStore
{
    public event Action? OnSeasonsChanged;
    public event Action? OnWrappedChanged;

    public GetWrappedSeasonsResult? Seasons { get; private set; }
    public SeasonWrappedResult? Wrapped { get; private set; }
    public HttpResult? WrappedError { get; private set; }

    public Task LoadSeasons() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetWrappedSeasons(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Seasons = response.Dto;
            OnSeasonsChanged?.Invoke();
        }
    }, nameof(LoadSeasons));

    public Task LoadWrapped(int seasonId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Wrapped?.SeasonId != seasonId)
        {
            Wrapped = null;
            WrappedError = null;
            OnWrappedChanged?.Invoke();
        }

        var response = await httpClient.GetSeasonWrapped(seasonId, cancellationToken);
        Wrapped = response is { Success: true, Dto: not null } ? response.Dto : null;
        WrappedError = response.Success ? null : response;
        OnWrappedChanged?.Invoke();
    }, nameof(LoadWrapped));

    public Task<HttpResult> Publish(int seasonId)
        => RunInSavingContextAsync(async cancellationToken => await Reload(await httpClient.PublishSeasonWrapped(seasonId, cancellationToken)), nameof(Publish));

    public Task<HttpResult> Unpublish(int seasonId)
        => RunInSavingContextAsync(async cancellationToken => await Reload(await httpClient.UnpublishSeasonWrapped(seasonId, cancellationToken)), nameof(Unpublish));

    private async Task<HttpResult> Reload(HttpResult response)
    {
        if (response.Success)
            await LoadSeasons();
        return response;
    }
}
