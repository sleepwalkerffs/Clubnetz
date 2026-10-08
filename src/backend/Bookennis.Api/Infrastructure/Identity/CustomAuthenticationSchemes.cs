using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Infrastructure.Identity;

public static class CustomAuthenticationSchemes
{
    public static readonly string Cookie = IdentityConstants.ApplicationScheme;
}