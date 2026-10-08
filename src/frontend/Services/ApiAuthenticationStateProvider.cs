using System.Globalization;
using System.Security.Claims;
using Bookennis.Client.Services.HttpClients.Account;
using Bookennis.Client.Services.UserAccessor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookennis.Client.Services;

public interface IAuthStateChanged
{
    public void AuthStateChanged();
}

public class ApiAuthenticationStateProvider(IAccountHttpClient accountHttpClient) : AuthenticationStateProvider, IAuthStateChanged, IClaimsUserDataAccessor
{
    public void AuthStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var result = await accountHttpClient.GetAuthenticatedUser();

        // anonymous authentications state
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        if (result.Success && result.Dto is not null)
        {
            var identity = new ClaimsIdentity("Bookennis");
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, result.Dto.Id.ToString(CultureInfo.InvariantCulture)));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name, result.Dto.Name));
            identity.AddClaim(new Claim(ClaimTypes.Name, result.Dto.Name));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.FamilyName, result.Dto.LastName));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.GivenName, result.Dto.FirstName));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, result.Dto.Email ?? "no email"));

            foreach (var role in result.Dto.Roles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));

            authState = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        ReadUserDataFromClaims(authState);

        return authState;
    }

    private void ReadUserDataFromClaims(AuthenticationState authState)
    {
        Id = null;
        Name = null;
        FirstName = null;
        LastName = null;
        Email = null;
        Salutation = null;

        if (authState.User.Identity is not null && authState.User.Identity.IsAuthenticated)
        {
            Id = Convert.ToInt32(authState.User.FindFirst(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value, CultureInfo.InvariantCulture);
            Name = authState.User.FindFirst(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;
            FirstName = authState.User.FindFirst(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value;
            LastName = authState.User.FindFirst(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value;
            Email = authState.User.FindFirst(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value;
            Salutation = authState.User.FindFirst(c => c.Type == JwtRegisteredClaimNames.Gender)?.Value;
        }
    }

    public int? Id { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? Email { get; private set; }

    public string? Salutation { get; private set; }

    public string? Name { get; private set; }
}