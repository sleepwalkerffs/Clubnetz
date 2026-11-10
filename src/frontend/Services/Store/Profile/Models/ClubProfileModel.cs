using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Profile.Models;

public record ClubProfileModel
{
    public required string ClubName { get; set; }
    public required int ClubId { get; init; }
    public required int MemberId { get; init; }
    public required MemberRole[] MemberRole { get; init; }

    /// <summary>An application administrator who opened a club they are not a member of: <see cref="MemberId"/> is 0.</summary>
    public bool IsSupportMode { get; init; }

    /// <summary>See <c>GetClubProfileResult.IsActiveMember</c>: enrolled in the active season, parent of an enrolled child, staff or support mode.</summary>
    public bool IsActiveMember { get; init; }

    /// <summary>See <c>GetClubProfileResult.Permissions</c>: what the member may do in the club. A convenience for the UI, the server decides.</summary>
    public List<Bookennis.Shared.Controller.Permissions.Permission> Permissions { get; init; } = [];

    /// <summary>See <c>GetClubProfileResult.PermissionScopes</c>: permissions that hold only for some courts or play modes, or not for some.</summary>
    public List<Bookennis.Shared.Controller.Permissions.PermissionScopeDto> PermissionScopes { get; init; } = [];
}