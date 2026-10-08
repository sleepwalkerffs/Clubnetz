using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Notifications;

namespace Bookennis.Client.Services.Store.Notifications;

/// <summary>On which channels (push, email) the signed-in user is notified about what. The preferences belong to the account, not the device.</summary>
public interface INotificationPreferencesStore : ISemaphoreStore
{
    event Action? OnPreferencesChanged;

    /// <summary>One entry per notification type; empty until loaded.</summary>
    IReadOnlyList<NotificationPreferenceDto> Preferences { get; }

    Task Load();

    /// <summary>Saves the channels of the given types. On failure the previous channels are kept.</summary>
    Task<HttpResult> Update(IReadOnlyCollection<NotificationPreferenceDto> changes);
}
