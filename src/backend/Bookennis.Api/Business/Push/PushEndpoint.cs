namespace Bookennis.Api.Business.Push;

public static class PushEndpoint
{
    // The server posts to the endpoint a client hands in. Only the push services of the browser vendors
    // are accepted, so a subscription can't be used to make the server call arbitrary hosts (SSRF).
    private static readonly string[] AllowedHosts =
    [
        "fcm.googleapis.com", // Chrome, Edge on Android, Samsung Internet, Opera, Brave
        "push.apple.com", // Safari, installed web apps on iOS
        "push.services.mozilla.com", // Firefox
        "notify.windows.com", // Edge on Windows
    ];

    public static bool IsAllowed(string? endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort
            && AllowedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    /// <summary>Host of the push service, shown in the personal data export instead of the full endpoint.</summary>
    public static string Host(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Host : "";
}
