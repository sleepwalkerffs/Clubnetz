using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Bookennis.Api.Business.ClubApiKeys;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;

namespace Bookennis.Api.Infrastructure.Identity;

/// <summary>
/// Authenticates requests that carry a club API key (<c>Authorization: Bearer cnz_...</c>). The request is then handled as the user who
/// created the key, but only within the key's club: a key is rejected on every route that does not belong to its club.
/// Only the policies that list <see cref="CustomAuthenticationSchemes.ClubApiKey"/> accept a key at all.
/// </summary>
public class ClubApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IMediator mediator) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[HeaderNames.Authorization].ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header[BearerPrefix.Length..].Trim();
        if (!ClubApiKey.IsToken(token))
            return AuthenticateResult.NoResult();

        // Without a club in the route the tenant filter would not restrict anything, so a key never works there
        if (ITenantService.GetTenantIdFromPath(Request.Path) is not { } clubId)
            return AuthenticateResult.Fail("An API key can only be used for the routes of its club.");

        var key = await mediator.Send(new AuthenticateClubApiKey(token, clubId), Context.RequestAborted);
        if (key is null)
            return AuthenticateResult.Fail("The API key is invalid, expired or does not belong to this club.");

        // Always the role of a normal user: a key never gets the rights of an application administrator
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, key.UserId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, nameof(UserRoles.User)),
                new Claim(ClubApiKeyClaims.KeyId, key.ClubApiKeyId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClubApiKeyClaims.ClubId, key.ClubId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClubApiKeyClaims.ReadOnly, key.IsReadOnly ? "true" : "false"),
            ],
            Scheme.Name,
            JwtRegisteredClaimNames.Sub,
            ClaimTypes.Role);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
