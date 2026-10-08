namespace Bookennis.Shared.Controller.Shared;

/// <summary>
/// Response header of every API call with the version of the deployed app. The client compares it
/// with its own version to notice that it is running an outdated build.
/// </summary>
public static class AppVersionHeader
{
    public const string Name = "X-App-Version";
}
