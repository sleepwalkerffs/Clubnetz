using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Push;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Push;
using Microsoft.JSInterop;

namespace Bookennis.Client.Services.Store.Push;

/// <summary>
/// Push notifications and installation of the app on this device. The browser side lives in wwwroot/pwa.js
/// (App.push, App.install), the notifications are shown by wwwroot/service-worker.js.
/// </summary>
public sealed class PushStore(IPushHttpClient pushHttpClient, IJSRuntime jsRuntime) : SemaphoreStore, IPushStore
{
    private string? publicKey;

    public event Action? OnPushChanged;

    public PushState State { get; private set; } = PushState.Unknown;
    public InstallState InstallState { get; private set; } = InstallState.None;
    public bool PushServiceBlockedByBrowser { get; private set; }

    public Task Load()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            InstallState = await GetInstallState();
            PushServiceBlockedByBrowser = await jsRuntime.InvokeAsync<bool>("App.push.isBrave", cancellationToken);

            var configuration = await pushHttpClient.GetConfiguration(cancellationToken);
            if (!configuration.Success)
                return;

            publicKey = configuration.Dto?.PublicKey;
            if (publicKey is null)
            {
                State = PushState.Unsupported;
                OnPushChanged?.Invoke();
                return;
            }

            try
            {
                var subscription = await jsRuntime.InvokeAsync<SavePushSubscriptionModel?>("App.push.restore", cancellationToken, publicKey);
                if (subscription is not null)
                    await pushHttpClient.SaveSubscription(subscription, cancellationToken);
            }
            catch (JSException)
            {
                // The push service is not reachable right now, the next start tries again
            }

            State = await GetState();
            OnPushChanged?.Invoke();
        }, nameof(Load));

    public Task<HttpResult> Enable()
        => RunInSavingContextAsync(async cancellationToken =>
        {
            if (publicKey is null)
                return HttpResult.OkResult;

            // First thing after the click: the permission dialog only opens on a user gesture
            SavePushSubscriptionModel? subscription;
            try
            {
                subscription = await jsRuntime.InvokeAsync<SavePushSubscriptionModel?>("App.push.subscribe", cancellationToken, publicKey);
            }
            catch (JSException e)
            {
                // E.g. the service worker could not be registered. The reason ends up in the browser console,
                // the caller sees that the state is still "off".
                Console.Error.WriteLine($"Push notifications could not be switched on: {e.Message}");
                subscription = null;
            }

            var result = HttpResult.OkResult;
            if (subscription is not null)
            {
                result = await pushHttpClient.SaveSubscription(subscription, cancellationToken);
                if (!result.Success)
                    await jsRuntime.InvokeAsync<string?>("App.push.unsubscribe", cancellationToken);
            }

            State = await GetState();
            OnPushChanged?.Invoke();
            return result;
        }, nameof(Enable));

    public Task<HttpResult> Disable()
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var endpoint = await jsRuntime.InvokeAsync<string?>("App.push.unsubscribe", cancellationToken);
            var result = endpoint is null
                ? HttpResult.OkResult
                : await pushHttpClient.DeleteSubscription(new DeletePushSubscriptionModel { Endpoint = endpoint }, cancellationToken);

            State = await GetState();
            OnPushChanged?.Invoke();
            return result;
        }, nameof(Disable));

    public Task<HttpResult> SendTest()
        => RunInSavingContextAsync(cancellationToken => pushHttpClient.SendTest(cancellationToken), nameof(SendTest));

    public Task DetachDevice()
        => RunInSavingContextAsync(async cancellationToken =>
        {
            try
            {
                var endpoint = await jsRuntime.InvokeAsync<string?>("App.push.getEndpoint", cancellationToken);
                if (endpoint is not null)
                    await pushHttpClient.DeleteSubscription(new DeletePushSubscriptionModel { Endpoint = endpoint }, cancellationToken);
            }
            catch (Exception e) when (e is JSException or HttpRequestException)
            {
                // Must never keep the user from signing out. The next user who signs in on this device takes the subscription over.
            }

            State = PushState.Unknown;
        }, nameof(DetachDevice));

    public async Task Install()
    {
        await jsRuntime.InvokeAsync<bool>("App.install.prompt");
        InstallState = await GetInstallState();
        OnPushChanged?.Invoke();
    }

    private async Task<PushState> GetState()
    {
        if (publicKey is null)
            return PushState.Unsupported;

        return await jsRuntime.InvokeAsync<string>("App.push.getState") switch
        {
            "on" => PushState.On,
            "off" => PushState.Off,
            "denied" => PushState.Denied,
            "needs-install" => PushState.NeedsInstall,
            _ => PushState.Unsupported
        };
    }

    private async Task<InstallState> GetInstallState()
        => await jsRuntime.InvokeAsync<string>("App.install.getState") switch
        {
            "installed" => InstallState.Installed,
            "prompt" => InstallState.Prompt,
            "ios" => InstallState.Ios,
            _ => InstallState.None
        };
}
