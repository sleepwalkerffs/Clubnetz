namespace Bookennis.Domain.Permissions;

/// <summary>
/// Something a member may do or see in a club. Every permission can be given to roles by the club admin
/// (<see cref="RolePermission"/>) and to single members, down to single resources (<see cref="PermissionGrant"/>).
/// The values are stored as text, the names must not change. <c>Bookennis.Shared.Controller.Permissions.Permission</c> mirrors this enum.
/// </summary>
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

public enum PermissionResourceType
{
    Court = 0,
    PlayMode = 1,
}

public enum PermissionEffect
{
    Allow = 0,
    Deny = 1,
}

public enum PermissionArea
{
    Members = 0,
    Planning = 1,
    Bookings = 2,
    Money = 3,
}
