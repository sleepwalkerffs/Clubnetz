using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Demo;

namespace Bookennis.Client.Services.Store.Demo;

/// <summary>Whether this installation is the public demo (banner, accounts on the login page). Loaded once per app start.</summary>
public interface IDemoStore : ISemaphoreStore
{
    event Action? OnDemoChanged;

    /// <summary><c>null</c> until loaded.</summary>
    GetDemoInfoResult? Info { get; }

    bool IsDemo { get; }

    /// <summary>Loads the info once. Further calls wait for the same request.</summary>
    Task Load();
}
