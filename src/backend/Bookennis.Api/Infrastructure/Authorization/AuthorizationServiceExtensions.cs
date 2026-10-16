using Bookennis.Api.Infrastructure.Exceptions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization;

public static class AuthorizationServiceExtensions
{
    public static async Task EnsureSucceeded(this Task<AuthorizationResult> result) => (await result).EnsureSucceeded();

    /// <summary>The caller is an active member of the club of the request (<see cref="AuthorizationPolicies.ActiveMember"/>).</summary>
    public static async Task<bool> IsActiveMember(this IAuthorizationService authorizationService, ClaimsPrincipal user)
        => (await authorizationService.AuthorizeAsync(user, AuthorizationPolicies.ActiveMember)).Succeeded;

    /// <summary>
    /// Members see their own data, and the data of other members only when they are active members:
    /// passes for the member themself (or a club admin) and for active members, otherwise 403.
    /// </summary>
    public static async Task EnsureSelfOrActiveMember(this IAuthorizationService authorizationService, ClaimsPrincipal user, int memberId)
    {
        if ((await authorizationService.AuthorizeAsync(user, new Models.MemberAuthorizationModel(memberId), AuthorizationPolicies.Member)).Succeeded)
            return;

        (await authorizationService.AuthorizeAsync(user, AuthorizationPolicies.ActiveMember)).EnsureSucceeded();
    }

    public static void EnsureSucceeded(this AuthorizationResult result)
    {
        if (!result.Succeeded)
        {
            var failedRequirements = result.Failure == null
                ? string.Empty
                : string.Join(", ", result.Failure.FailedRequirements.Select(r => r.GetType().Name));

            throw new AccessDeniedException($"Authorization failed for some requirements: {failedRequirements}");
        }
    }
}

