using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Wrapped;

namespace Bookennis.Client.Services.Store.Wrapped;

public interface IWrappedStore : ISemaphoreStore
{
    public event Action? OnSeasonsChanged;
    public event Action? OnWrappedChanged;

    /// <summary>The seasons whose Wrapped the member can open (list page and My Club teaser).</summary>
    public GetWrappedSeasonsResult? Seasons { get; }

    /// <summary>The Wrapped that is open in the story player.</summary>
    public SeasonWrappedResult? Wrapped { get; }

    /// <summary>Why <see cref="Wrapped"/> could not be loaded (e.g. not released), <c>null</c> when it was.</summary>
    public HttpResult? WrappedError { get; }

    public Task LoadSeasons();
    public Task LoadWrapped(int seasonId);
    public Task<HttpResult> Publish(int seasonId);
    public Task<HttpResult> Unpublish(int seasonId);
}
