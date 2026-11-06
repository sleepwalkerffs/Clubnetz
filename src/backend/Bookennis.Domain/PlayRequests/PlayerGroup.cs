using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;
using Bookennis.Global;

namespace Bookennis.Domain.PlayRequests;

/// <summary>
/// The reusable list of players of one member in one club ("my Tuesday group"). It is personal: only its owner sees and changes it.
/// A request starts from a group but can add or remove players without changing the group.
/// </summary>
public class PlayerGroup : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNameLength = 60;
    public const int MaxMembers = PlayRequest.MaxInvitees;
    public const int MaxGroupsPerMember = 20;

    public enum ErrorCode
    {
        PlayerGroupNameRequired = 0,
        PlayerGroupNameTooLong = 1,
        PlayerGroupTooManyMembers = 2,
        PlayerGroupTooMany = 3,
        PlayerGroupEmpty = 4,
    }

    private readonly List<PlayerGroupMember> members = new();

#pragma warning disable CS8618
    private PlayerGroup() { }
#pragma warning restore CS8618

    public PlayerGroup(int clubId, int ownerMemberId, string name, IReadOnlyCollection<int> memberIds)
    {
        ClubId = clubId;
        OwnerMemberId = ownerMemberId;
        Update(name, memberIds);
    }

    public int OwnerMemberId { get; private set; }
    public string Name { get; private set; } = "";
    public IReadOnlyList<PlayerGroupMember> Members => members.AsReadOnly();

    public void Update(string name, IReadOnlyCollection<int> memberIds)
    {
        var cleaned = name.CleanNullable();
        if (cleaned is null)
            throw new PreconditionException(ErrorCode.PlayerGroupNameRequired, "A group needs a name");

        if (cleaned.Length > MaxNameLength)
            throw new PreconditionException(ErrorCode.PlayerGroupNameTooLong, "The name is too long");

        var ids = memberIds.Distinct().Where(id => id != OwnerMemberId).ToList();
        if (ids.Count == 0)
            throw new PreconditionException(ErrorCode.PlayerGroupEmpty, "A group needs at least one player");

        if (ids.Count > MaxMembers)
            throw new PreconditionException(ErrorCode.PlayerGroupTooManyMembers, "Too many players in the group");

        Name = cleaned;
        members.RemoveAll(m => !ids.Contains(m.MemberId));
        foreach (var id in ids.Where(id => members.All(m => m.MemberId != id)))
            members.Add(new PlayerGroupMember(this, id));
    }
}

public class PlayerGroupMember : DomainEntity
{
#pragma warning disable CS8618
    private PlayerGroupMember() { }
#pragma warning restore CS8618

    internal PlayerGroupMember(PlayerGroup group, int memberId)
    {
        Group = group;
        PlayerGroupId = group.Id;
        MemberId = memberId;
    }

    public PlayerGroup Group { get; private set; }
    public int PlayerGroupId { get; private set; }
    public int MemberId { get; private set; }
}
