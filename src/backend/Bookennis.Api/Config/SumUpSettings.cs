namespace Bookennis.Api.Config;

/// <summary>
/// The OAuth app of this installation at SumUp. Clubhouse hosts connect their own SumUp account to it, and members then pay their
/// tabs in the app with the hosted checkout of SumUp. Switched off everywhere as long as it is not configured: environment variables
/// <c>SumUp__ClientId</c> and <c>SumUp__ClientSecret</c>. The redirect URL registered at SumUp is <c>{AppUrl}api/SumUp/Callback</c>.
/// </summary>
public class SumUpSettings
{
    public string? ClientId { get; set; }

    /// <summary>Secret. Never commit it.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>The API and the OAuth endpoints (<c>/authorize</c>, <c>/token</c>). The same for sandbox and live accounts.</summary>
    public string ApiUrl { get; set; } = "https://api.sumup.com";

    /// <summary>
    /// What the app asks the host for. <c>payments</c> must be approved by SumUp for the OAuth app; <c>user.profile_readonly</c>
    /// reads the merchant code.
    /// </summary>
    public string Scopes { get; set; } = "payments user.profile_readonly";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
