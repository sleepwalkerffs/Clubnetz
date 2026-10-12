using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Requirements;

/// <summary>
/// Access to one family (<see cref="Models.FamilyAuthorizationModel"/>): its members may read it, only its parents may change it.
/// Admins of the club may do both.
/// </summary>
public class FamilyAccessRequirement : IAuthorizationRequirement
{
    public required bool ParentsOnly { get; init; }
}
