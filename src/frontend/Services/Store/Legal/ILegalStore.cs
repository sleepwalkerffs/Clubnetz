using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Legal;

namespace Bookennis.Client.Services.Store.Legal;

/// <summary>Operator details shown on the imprint and privacy policy pages. They are configured on the server.</summary>
public interface ILegalStore : ISemaphoreStore
{
    event Action? OnLegalChanged;

    /// <summary><c>null</c> until loaded.</summary>
    GetLegalSettingsResult? Settings { get; }

    /// <summary>Loaded, but the operator has not been configured on the server.</summary>
    bool IsNotConfigured { get; }

    /// <summary>Loads the details once. Further calls wait for the same request.</summary>
    Task Load();
}
