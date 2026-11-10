using Bookennis.Domain.Base;

namespace Bookennis.Domain.Permissions;

/// <summary>
/// An extra permission (or a taken one) for a single member, optionally for one resource only ("member X may block court 3").
/// Evaluation: an explicit deny wins over an allow, a grant for the resource wins over the roles. Never applies to the Admin role.
/// Personal data, so it is part of the export of the member.
/// </summary>
public class PermissionGrant : TenantDomainEntity, IAggregateRoot
{
    public const int NoteMaxLength = 200;

#pragma warning disable CS8618
    private PermissionGrant() { }
#pragma warning restore CS8618

    public PermissionGrant(int clubId, int memberId, Permission permission, PermissionResourceType? resourceType, int? resourceId, PermissionEffect effect, int? grantedByMemberId, string? note)
    {
        if ((resourceType is null) != (resourceId is null))
            throw new ArgumentException("A resource needs a type and an id.");
        if (resourceType is { } type && PermissionCatalog.Get(permission).ResourceType != type)
            throw new ArgumentException("The permission can't be limited to this kind of resource.");

        ClubId = clubId;
        MemberId = memberId;
        Permission = permission;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Effect = effect;
        GrantedByMemberId = grantedByMemberId;
        var trimmed = note?.Trim();
        Note = string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, NoteMaxLength)];
    }

    public int MemberId { get; private set; }
    public Permission Permission { get; private set; }
    public PermissionResourceType? ResourceType { get; private set; }
    public int? ResourceId { get; private set; }
    public PermissionEffect Effect { get; private set; }
    public int? GrantedByMemberId { get; private set; }
    public string? Note { get; private set; }
}
