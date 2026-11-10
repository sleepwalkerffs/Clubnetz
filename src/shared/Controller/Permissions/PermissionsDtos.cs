using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Permissions;

/// <summary>Mirrors <c>Bookennis.Domain.Permissions.Permission</c> (same names and values).</summary>
#pragma warning disable CA1711 // The domain word is "permission"
public enum Permission
#pragma warning restore CA1711
{
    MembersView = 0,
    StatisticsView = 1,
    EventsManage = 2,
    AnnouncementsManage = 3,
    CourtBlockingsManage = 4,
    OneTimeBadgesManage = 5,
    BookingsManageForAnyMember = 6,
    PaymentsManage = 7,
    CourtSalesManage = 8,
    ClubhouseHost = 9,
    AccountingRead = 10,
    AccountingManage = 11,
    AccountingAudit = 12,
}

public enum PermissionArea { Members = 0, Planning = 1, Bookings = 2, Money = 3 }

public enum PermissionResourceType { Court = 0, PlayMode = 1 }

public enum PermissionEffect { Allow = 0, Deny = 1 }

public record PermissionDto
{
    public required Permission Permission { get; init; }
    public required PermissionArea Area { get; init; }
    public required bool IsMoney { get; init; }
    public required PermissionResourceType? ResourceType { get; init; }
}

public record RolePermissionsDto
{
    public required MemberRole Role { get; init; }
    public required List<Permission> Permissions { get; init; }
    public required List<Permission> DefaultPermissions { get; init; }
}

/// <summary>The matrix of the club. Admin always has everything and is not listed in <see cref="Roles"/>.</summary>
public record GetRolePermissionsResult
{
    public required List<PermissionDto> Catalog { get; init; }
    public required List<RolePermissionsDto> Roles { get; init; }
}

public record UpdateRolePermissionsRequest
{
    public required List<Permission> Permissions { get; init; }
}

public record PermissionGrantDto
{
    public required int Id { get; init; }
    public required int MemberId { get; init; }
    public required string MemberName { get; init; }
    public required Permission Permission { get; init; }
    public required PermissionResourceType? ResourceType { get; init; }
    public required int? ResourceId { get; init; }
    public required string? ResourceName { get; init; }
    public required PermissionEffect Effect { get; init; }
    public required string? Note { get; init; }
}

public record GetPermissionGrantsResult
{
    public required List<PermissionGrantDto> Grants { get; init; }
}

public record CreatePermissionGrantRequest
{
    public required int MemberId { get; init; }
    public required Permission Permission { get; init; }
    public PermissionResourceType? ResourceType { get; init; }
    public int? ResourceId { get; init; }
    public required PermissionEffect Effect { get; init; }
    public string? Note { get; init; }
}
