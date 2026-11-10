using Bookennis.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Requirements;

/// <summary>
/// The member needs a <see cref="Permission"/> in the club. Who has it is decided by the role matrix and the grants of the club
/// (see <c>PermissionEvaluator</c>), not by this requirement. The policies that carry it keep their names and their cookie-only rules.
/// </summary>
public record PermissionRequirement(Permission Permission) : IAuthorizationRequirement;
