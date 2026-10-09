using Bookennis.Client.Services.HttpClients.Legal;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Legal;

namespace Bookennis.Client.Services.Store.Legal;

public sealed class LegalStore(ILegalHttpClient legalHttpClient) : SemaphoreStore, ILegalStore
{
    private Task? loadTask;

    public event Action? OnLegalChanged;

    public GetLegalSettingsResult? Settings { get; private set; }

    public bool IsNotConfigured
        => Settings is not null && (string.IsNullOrWhiteSpace(Settings.OperatorName) || string.IsNullOrWhiteSpace(Settings.Email));

    // The components of a legal page all ask for the details at the same time, they share one request
    public Task Load() => loadTask ??= LoadSettings();

    private Task LoadSettings()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await legalHttpClient.GetSettings(cancellationToken);
            if (!result.Success || result.Dto is null)
            {
                // Try again the next time a legal page is opened
                loadTask = null;
                return;
            }

            Settings = result.Dto;
            OnLegalChanged?.Invoke();
        }, nameof(Load));
}
