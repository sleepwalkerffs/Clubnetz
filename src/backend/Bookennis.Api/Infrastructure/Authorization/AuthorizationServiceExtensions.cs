using Bookennis.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization;

public static class AuthorizationServiceExtensions
{
    public static async Task EnsureSucceeded(this Task<AuthorizationResult> result) => (await result).EnsureSucceeded();

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

