using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Infrastructure.Identity;

public static class CustomAuthenticationSchemes
{
    public static readonly string Cookie = IdentityConstants.ApplicationScheme;

    /// <summary>A club API key sent as bearer token, see <see cref="ClubApiKeyAuthenticationHandler"/>.</summary>
    public const string ClubApiKey = nameof(ClubApiKey);

    /// <summary>What the club policies accept: a signed-in user or an API key of the club.</summary>
    public static readonly string[] CookieOrClubApiKey = [Cookie, ClubApiKey];
}
