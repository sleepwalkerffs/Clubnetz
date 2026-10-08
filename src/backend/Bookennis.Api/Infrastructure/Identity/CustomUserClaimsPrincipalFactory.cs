using System.Globalization;
using System.Security.Claims;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookennis.Api.Infrastructure.Identity;

public class CustomUserClaimsPrincipalFactory(
    UserManager<Domain.User.User> userManager,
    RoleManager<UserRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
    : UserClaimsPrincipalFactory<Domain.User.User, UserRole>(userManager, roleManager, optionsAccessor)
#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Domain.User.User user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName));
        identity.AddClaim(
            new Claim(
                JwtRegisteredClaimNames.Gender,
                user.Gender == Gender.Male
                    ? "male"
                    : user.Gender == Gender.Female
                        ? "female"
                        : "diverse"
            )
        );

        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, user.Email ?? "no email"));

        return identity;
    }
}