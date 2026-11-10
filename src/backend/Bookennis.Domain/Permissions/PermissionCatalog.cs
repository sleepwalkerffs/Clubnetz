using Bookennis.Domain.Members;

namespace Bookennis.Domain.Permissions;

/// <summary>What a permission is: its area, whether it is money (cookie only), the resource it can be limited to and what each role gets by default.</summary>
public record PermissionInfo(Permission Permission, PermissionArea Area, bool IsMoney, PermissionResourceType? ResourceType, MemberRole[] DefaultRoles);

/// <summary>
/// The one place that knows the permissions and today's defaults. <see cref="MemberRole.Admin"/> always has every permission and can't be changed.
/// Application administrators (support mode) pass everything as before.
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionInfo> All =
    [
        new(Permission.MembersView, PermissionArea.Members, false, null, [MemberRole.Maintainer]),
        new(Permission.StatisticsView, PermissionArea.Members, false, null, [MemberRole.Maintainer]),
        new(Permission.EventsManage, PermissionArea.Planning, false, null, [MemberRole.Maintainer, MemberRole.SportsDirector, MemberRole.YouthSportsDirector]),
        new(Permission.AnnouncementsManage, PermissionArea.Planning, false, null, [MemberRole.Maintainer, MemberRole.SportsDirector, MemberRole.YouthSportsDirector]),
        new(Permission.OneTimeBadgesManage, PermissionArea.Planning, false, null, [MemberRole.SportsDirector, MemberRole.YouthSportsDirector]),
        new(Permission.CourtBlockingsManage, PermissionArea.Bookings, false, PermissionResourceType.Court, [MemberRole.Maintainer, MemberRole.SportsDirector]),
        new(Permission.BookingsManageForAnyMember, PermissionArea.Bookings, false, PermissionResourceType.PlayMode, [MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Trainer]),
        new(Permission.PaymentsManage, PermissionArea.Money, true, null, [MemberRole.Treasurer]),
        new(Permission.CourtSalesManage, PermissionArea.Money, true, null, [MemberRole.Treasurer]),
        new(Permission.ClubhouseHost, PermissionArea.Money, true, null, [MemberRole.ClubhouseHost]),
        new(Permission.AccountingRead, PermissionArea.Money, true, null, [MemberRole.Treasurer, MemberRole.Auditor]),
        new(Permission.AccountingManage, PermissionArea.Money, true, null, [MemberRole.Treasurer]),
        new(Permission.AccountingAudit, PermissionArea.Money, true, null, [MemberRole.Auditor]),
    ];

    /// <summary>Roles whose permissions the admin can edit. Admin is locked (everything), Guest is history only.</summary>
    public static readonly IReadOnlyList<MemberRole> EditableRoles =
    [
        MemberRole.Maintainer, MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Trainer,
        MemberRole.Treasurer, MemberRole.ClubhouseHost, MemberRole.Auditor, MemberRole.User,
    ];

    public static PermissionInfo Get(Permission permission) => All.Single(p => p.Permission == permission);

    public static bool IsEditableRole(MemberRole role) => EditableRoles.Contains(role);

    /// <summary>The defaults as the cells of the matrix.</summary>
    public static IEnumerable<(MemberRole Role, Permission Permission)> DefaultMatrix()
        => All.SelectMany(p => p.DefaultRoles.Where(IsEditableRole).Select(role => (role, p.Permission)));
}
