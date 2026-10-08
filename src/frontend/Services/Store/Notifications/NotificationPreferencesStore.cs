using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Notifications;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Notifications;

namespace Bookennis.Client.Services.Store.Notifications;

public sealed class NotificationPreferencesStore(INotificationPreferencesHttpClient httpClient) : SemaphoreStore, INotificationPreferencesStore
{
    private List<NotificationPreferenceDto> preferences = [];

    public event Action? OnPreferencesChanged;

    public IReadOnlyList<NotificationPreferenceDto> Preferences => preferences;

    public Task Load()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.Get(cancellationToken);
            if (!result.Success || result.Dto is null)
                return;

            preferences = result.Dto.Preferences;
            OnPreferencesChanged?.Invoke();
        }, nameof(Load));

    public Task<HttpResult> Update(IReadOnlyCollection<NotificationPreferenceDto> changes)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.Update(new UpdateNotificationPreferencesModel { Preferences = [.. changes] }, cancellationToken);
            if (result.Success)
            {
                preferences = preferences
                    .Select(p => changes.FirstOrDefault(c => c.Type == p.Type) ?? p)
                    .ToList();
            }

            OnPreferencesChanged?.Invoke();
            return result;
        }, nameof(Update));
}
