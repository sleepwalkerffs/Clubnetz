using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;

namespace Bookennis.Client.Services.Store.Push;

/// <summary>Push notifications on this device.</summary>
public enum PushState
{
    /// <summary>Not determined yet.</summary>
    Unknown,

    /// <summary>The server has no push keys configured, or the browser can't do push notifications.</summary>
    Unsupported,

    /// <summary>iPhone/iPad: push notifications only work once the app is on the home screen.</summary>
    NeedsInstall,

    /// <summary>The user blocked notifications in the browser. Only the browser settings can undo that.</summary>
    Denied,

    Off,
    On,
}

/// <summary>Whether this device can put the app on its home screen.</summary>
public enum InstallState
{
    /// <summary>Nothing to offer (already installed, or the browser doesn't tell).</summary>
    None,

    Installed,

    /// <summary>The browser installs the app when asked.</summary>
    Prompt,

    /// <summary>iPhone/iPad: the user has to use "Share &gt; Add to Home Screen".</summary>
    Ios,
}

public interface IPushStore : ISemaphoreStore
{
    event Action? OnPushChanged;

    PushState State { get; }
    InstallState InstallState { get; }

    /// <summary>
    /// Brave: the push service is switched off in the browser settings by default, subscribing fails until
    /// the user allows "Google services for push messaging".
    /// </summary>
    bool PushServiceBlockedByBrowser { get; }

    /// <summary>
    /// Loads the state of this device and registers its subscription again if notifications are on
    /// (browsers replace subscriptions from time to time). Call after sign-in and when showing the settings.
    /// </summary>
    Task Load();

    /// <summary>Asks for permission and switches notifications on. Has to be called directly from a click.</summary>
    Task<HttpResult> Enable();

    Task<HttpResult> Disable();

    Task<HttpResult> SendTest();

    /// <summary>Stops notifications for the user who signs out, the device itself stays subscribed for the next sign-in.</summary>
    Task DetachDevice();

    /// <summary>Shows the browser's install dialog.</summary>
    Task Install();
}
