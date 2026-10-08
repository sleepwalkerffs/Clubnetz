using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Bookennis.Domain.User;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookennis.Api.Infrastructure.User;

[DebuggerStepThrough]
public static class ClaimsPrincipalExtensions
{
    public static int GetId(this ClaimsPrincipal principal) => int.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "", CultureInfo.InvariantCulture);

    public static bool TryGetId(this ClaimsPrincipal principal, out int id) => int.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out id);

    public static bool IsAuthenticated(this ClaimsPrincipal principal) => principal.Identity?.IsAuthenticated ?? false;

    public static bool IsAdministrator(this ClaimsPrincipal principal) => principal.IsInRole(nameof(UserRoles.Administrator));
}
