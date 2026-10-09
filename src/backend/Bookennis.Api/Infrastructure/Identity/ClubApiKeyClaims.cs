using System.Security.Claims;

namespace Bookennis.Api.Infrastructure.Identity;

/// <summary>Claims of a request that was authenticated with a club API key (see <see cref="ClubApiKeyAuthenticationHandler"/>).</summary>
public static class ClubApiKeyClaims
{
    public const string KeyId = "club_api_key_id";
    public const string ClubId = "club_api_key_club_id";
    public const string ReadOnly = "club_api_key_read_only";

    public static bool IsClubApiKey(this ClaimsPrincipal principal) => principal.HasClaim(c => c.Type == KeyId);

    public static string? GetClubApiKeyId(this ClaimsPrincipal principal) => principal.FindFirstValue(KeyId);

    public static bool IsReadOnlyClubApiKey(this ClaimsPrincipal principal) => principal.HasClaim(ReadOnly, "true");
}
