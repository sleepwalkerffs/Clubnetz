using Bookennis.Client.Services.HttpClients.Demo;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Demo;

namespace Bookennis.Client.Services.Store.Demo;

public sealed class DemoStore(IDemoHttpClient demoHttpClient) : SemaphoreStore, IDemoStore
{
    private Task? loadTask;

    public event Action? OnDemoChanged;

    public GetDemoInfoResult? Info { get; private set; }

    public bool IsDemo => Info?.IsEnabled == true;

    // The banner and the login page ask at the same time, they share one request
    public Task Load() => loadTask ??= LoadInfo();

    private Task LoadInfo()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await demoHttpClient.GetInfo(cancellationToken);
            if (!result.Success || result.Dto is null)
            {
                loadTask = null;
                return;
            }

            Info = result.Dto;
            OnDemoChanged?.Invoke();
        }, nameof(Load));
}
