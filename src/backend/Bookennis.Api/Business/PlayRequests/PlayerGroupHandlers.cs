using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.PlayRequests;
using Bookennis.Shared.Controller.PlayRequests;
using Fusonic.Extensions.Common.Entities;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.PlayRequests;

internal static class PlayerGroupQueries
{
    /// <summary>The groups of the member with the names of their players, by name.</summary>
    public static async Task<List<PlayerGroupDto>> Load(AppDbContext context, int clubId, int ownerMemberId, int? groupId, CancellationToken cancellationToken)
    {
        var groups = await context.PlayerGroups
            .Where(g => g.ClubId == clubId && g.OwnerMemberId == ownerMemberId && (groupId == null || g.Id == groupId))
            .Select(g => new { g.Id, g.Name, MemberIds = g.Members.Select(m => m.MemberId).ToList() })
            .OrderBy(g => g.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var memberIds = groups.SelectMany(g => g.MemberIds).Distinct().ToList();
        var names = await (
            from member in context.ClubMembers
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == clubId && memberIds.Contains(member.Id)
            select new PlayerGroupMemberDto(member.Id, user.FirstName, user.LastName)
        ).ToDictionaryAsync(m => m.MemberId, cancellationToken);

        return groups
            .Select(g => new PlayerGroupDto(
                g.Id,
                g.Name,
                g.MemberIds.Where(names.ContainsKey).Select(id => names[id]).OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList()))
            .ToList();
    }

    /// <summary>The players of a group are members of the club (not necessarily active: a group can outlive a season).</summary>
    public static async Task EnsureClubMembers(AppDbContext context, int clubId, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
    {
        var distinct = memberIds.Distinct().ToList();
        var found = await context.ClubMembers.CountAsync(m => m.ClubId == clubId && distinct.Contains(m.Id), cancellationToken);
        if (found != distinct.Count)
            throw new EntityNotFoundException("Player does not exist");
    }
}

/// <summary>The saved groups of the caller. Groups are personal: nobody else sees them.</summary>
public record GetPlayerGroups(int ClubId, int UserId) : IQuery<List<PlayerGroupDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPlayerGroups, List<PlayerGroupDto>>
    {
        public async Task<List<PlayerGroupDto>> Handle(GetPlayerGroups request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            return await PlayerGroupQueries.Load(context, request.ClubId, memberId, null, cancellationToken);
        }
    }
}

public record CreatePlayerGroup(int ClubId, int UserId, string Name, List<int> MemberIds) : ICommand<PlayerGroupDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreatePlayerGroup, PlayerGroupDto>
    {
        public async Task<PlayerGroupDto> Handle(CreatePlayerGroup request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var count = await context.PlayerGroups.CountAsync(g => g.ClubId == request.ClubId && g.OwnerMemberId == memberId, cancellationToken);
            if (count >= PlayerGroup.MaxGroupsPerMember)
                throw new PreconditionException(PlayerGroup.ErrorCode.PlayerGroupTooMany, "Too many groups");

            await PlayerGroupQueries.EnsureClubMembers(context, request.ClubId, request.MemberIds, cancellationToken);

            var group = context.Add(new PlayerGroup(request.ClubId, memberId, request.Name, request.MemberIds)).Entity;
            await context.SaveChangesAsync(cancellationToken);

            return (await PlayerGroupQueries.Load(context, request.ClubId, memberId, group.Id, cancellationToken)).Single();
        }
    }
}

public record UpdatePlayerGroup(int ClubId, int UserId, int GroupId, string Name, List<int> MemberIds) : ICommand<PlayerGroupDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdatePlayerGroup, PlayerGroupDto>
    {
        public async Task<PlayerGroupDto> Handle(UpdatePlayerGroup request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);

            // Somebody else's group does not exist for the caller
            var group = await context.PlayerGroups
                .Include(g => g.Members)
                .SingleRequiredAsync(g => g.Id == request.GroupId && g.ClubId == request.ClubId && g.OwnerMemberId == memberId, cancellationToken);

            await PlayerGroupQueries.EnsureClubMembers(context, request.ClubId, request.MemberIds, cancellationToken);

            group.Update(request.Name, request.MemberIds);
            await context.SaveChangesAsync(cancellationToken);

            return (await PlayerGroupQueries.Load(context, request.ClubId, memberId, group.Id, cancellationToken)).Single();
        }
    }
}

public record DeletePlayerGroup(int ClubId, int UserId, int GroupId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeletePlayerGroup>
    {
        public async Task<Unit> Handle(DeletePlayerGroup request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var group = await context.PlayerGroups
                .SingleRequiredAsync(g => g.Id == request.GroupId && g.ClubId == request.ClubId && g.OwnerMemberId == memberId, cancellationToken);

            context.PlayerGroups.Remove(group);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
