using System.Reflection;

namespace Bookennis.Client.Services.AppVersion;

public interface IAppVersionService
{
    /// <summary>Version the server reported when it differs from the version of this client, otherwise null.</summary>
    string? NewerServerVersion { get; }

    event Action? OnUpdateAvailable;

    void ReportServerVersion(string serverVersion);
}

/// <summary>
/// Notices that the server runs a different build than this client. An installed app is resumed from memory
/// for days, so without this check an old client would keep talking to a newer API after a deployment.
/// </summary>
public class AppVersionService : IAppVersionService
{
    // "1.0.0+<commit>", same value as AppVersionConfiguration.Version in the API
    private static readonly string ClientVersion = typeof(AppVersionService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    public string? NewerServerVersion { get; private set; }

    public event Action? OnUpdateAvailable;

    public void ReportServerVersion(string serverVersion)
    {
        if (string.IsNullOrEmpty(serverVersion) || serverVersion == ClientVersion || serverVersion == NewerServerVersion)
            return;

        NewerServerVersion = serverVersion;
        OnUpdateAvailable?.Invoke();
    }
}
