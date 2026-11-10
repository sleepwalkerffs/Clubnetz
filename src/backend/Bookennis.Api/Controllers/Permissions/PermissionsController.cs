using Bookennis.Api.Business.Permissions;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.Permissions;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DomainMemberRole = Bookennis.Domain.Members.MemberRole;
using DomainPermission = Bookennis.Domain.Permissions.Permission;

namespace Bookennis.Api.Controllers.Permissions;

/// <summary>
/// What the roles and single members may do in the club. Only a signed-in admin, never an API key.
/// Admin always has every permission and can't be changed, so the admins can't lock themselves out.
/// </summary>
[Authorize(AuthorizationPolicies.PermissionManager)]
public class PermissionsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet("roles")]
    public Task<GetRolePermissionsResult> GetRolePermissions(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetRolePermissions(clubId), cancellationToken);

    [HttpPut("roles/{role}")]
    public Task UpdateRolePermissions(int clubId, DomainMemberRole role, [FromBody] UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
        => mediator.Send(new UpdateRolePermissions(clubId, role, request.Permissions.Select(p => (DomainPermission)p).ToList()), cancellationToken);

    [HttpDelete("roles/{role}")]
    public Task ResetRolePermissions(int clubId, DomainMemberRole role, CancellationToken cancellationToken)
        => mediator.Send(new ResetRolePermissions(clubId, role), cancellationToken);

    [HttpDelete("roles")]
    public Task ResetAllRolePermissions(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new ResetRolePermissions(clubId, null), cancellationToken);

    [HttpGet("grants")]
    public Task<GetPermissionGrantsResult> GetPermissionGrants(int clubId, [FromQuery] int? memberId, CancellationToken cancellationToken)
        => mediator.Send(new GetPermissionGrants(clubId, memberId), cancellationToken);

    [HttpPost("grants")]
    public Task<int> CreatePermissionGrant(int clubId, [FromBody] CreatePermissionGrantRequest request, CancellationToken cancellationToken)
        => mediator.Send(new CreatePermissionGrant(
            clubId, userAccessor.GetUserId(), request.MemberId, (DomainPermission)request.Permission,
            request.ResourceType is { } type ? (Bookennis.Domain.Permissions.PermissionResourceType)type : null, request.ResourceId,
            (Bookennis.Domain.Permissions.PermissionEffect)request.Effect, request.Note), cancellationToken);

    [HttpDelete("grants/{grantId:int}")]
    public Task DeletePermissionGrant(int clubId, int grantId, CancellationToken cancellationToken)
        => mediator.Send(new DeletePermissionGrant(clubId, grantId), cancellationToken);
}
