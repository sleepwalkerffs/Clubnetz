using System.Security.Claims;
using Bookennis.Domain.User;
using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Bookennis.Api.Infrastructure.Configuration.Hangfire;

public class AdminRoleAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (!(httpContext?.User.Identity?.IsAuthenticated ?? false))
            return false;

        var roleClaims = httpContext.User.FindAll(ClaimTypes.Role).ToList().SelectMany(x => x.Value.Split(','));
        return roleClaims.Contains(nameof(UserRoles.Administrator));

    }
}
