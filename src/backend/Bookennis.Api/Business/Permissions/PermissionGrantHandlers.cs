using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
using Bookennis.Shared.Controller.Permissions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using DomainPermission = Bookennis.Domain.Permissions.Permission;
using PermissionEffect = Bookennis.Domain.Permissions.PermissionEffect;
using PermissionResourceType = Bookennis.Domain.Permissions.PermissionResourceType;
using SharedPermission = Bookennis.Shared.Controller.Permissions.Permission;

namespace Bookennis.Api.Business.Permissions;

/// <summary>The grants of the club (extra and taken permissions of single members), of one member or of all.</summary>
public record GetPermissionGrants(int ClubId, int? MemberId) : IQuery<GetPermissionGrantsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPermissionGrants, GetPermissionGrantsResult>
    {
        public async Task<GetPermissionGrantsResult> Handle(GetPermissionGrants request, CancellationToken cancellationToken)
        {
            var rows = await (from grant in context.PermissionGrants.IgnoreQueryFilters()
                              join member in context.Set<Member>().IgnoreQueryFilters() on grant.MemberId equals member.Id
                              join user in context.Users on member.UserId equals user.Id
                              where grant.ClubId == request.ClubId && (request.MemberId == null || grant.MemberId == request.MemberId)
                              orderby user.LastName, user.FirstName, grant.Id
                              select new
                              {
                                  grant.Id,
                                  grant.MemberId,
                                  user.FirstName,
                                  user.LastName,
                                  grant.Permission,
                                  grant.ResourceType,
                                  grant.ResourceId,
                                  grant.Effect,
                                  grant.Note,
                                  CourtName = context.Courts.IgnoreQueryFilters().Where(c => c.ClubId == request.ClubId && c.Id == grant.ResourceId).Select(c => c.Name).FirstOrDefault(),
                                  PlayModeName = context.PlayModes.IgnoreQueryFilters().Where(p => p.ClubId == request.ClubId && p.Id == grant.ResourceId).Select(p => p.Name).FirstOrDefault(),
                              }).ToListAsync(cancellationToken);

            return new GetPermissionGrantsResult
            {
                Grants = rows.Select(r => new PermissionGrantDto
                {
                    Id = r.Id,
                    MemberId = r.MemberId,
                    MemberName = $"{r.FirstName} {r.LastName}".Trim(),
                    Permission = (SharedPermission)r.Permission,
                    ResourceType = r.ResourceType is { } type ? (Bookennis.Shared.Controller.Permissions.PermissionResourceType)type : null,
                    ResourceId = r.ResourceId,
                    ResourceName = r.ResourceType == PermissionResourceType.Court ? r.CourtName : r.ResourceType == PermissionResourceType.PlayMode ? r.PlayModeName : null,
                    Effect = (Bookennis.Shared.Controller.Permissions.PermissionEffect)r.Effect,
                    Note = r.Note,
                }).ToList(),
            };
        }
    }
}

/// <summary>
/// Gives a member a permission, or takes it, for the whole club or for one court / play mode. The same member, permission and resource
/// keep one grant: giving it again changes the effect and the note.
/// </summary>
[SecurityAudit]
public record CreatePermissionGrant(
    int ClubId, int UserId, int MemberId, DomainPermission Permission, PermissionResourceType? ResourceType, int? ResourceId,
    PermissionEffect Effect, string? Note) : ICommand<int>
{
    public enum ErrorCode
    {
        PermissionGrantForAdmin = 0,
        PermissionGrantResourceInvalid = 1,
    }

    public class Handler(AppDbContext context) : IRequestHandler<CreatePermissionGrant, int>
    {
        public async Task<int> Handle(CreatePermissionGrant request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers.IgnoreQueryFilters()
                .SingleOrDefaultAsync(m => m.Id == request.MemberId && m.ClubId == request.ClubId, cancellationToken)
                ?? throw new EntityNotFoundException(typeof(Member), request.MemberId);

            // The admin has everything and can't be limited: a grant would only suggest otherwise
            if (member.UserRoles.Contains(MemberRole.Admin))
                throw new PreconditionException(ErrorCode.PermissionGrantForAdmin, "An admin always has every permission.");

            if ((request.ResourceType is null) != (request.ResourceId is null)
                || (request.ResourceType is { } type && PermissionCatalog.Get(request.Permission).ResourceType != type))
                throw new PreconditionException(ErrorCode.PermissionGrantResourceInvalid, "The permission can't be limited to this resource.");

            if (request.ResourceType == PermissionResourceType.Court
                && !await context.Courts.IgnoreQueryFilters().AnyAsync(c => c.ClubId == request.ClubId && c.Id == request.ResourceId, cancellationToken))
                throw new EntityNotFoundException(typeof(Bookennis.Domain.Courts.Court), request.ResourceId!);

            if (request.ResourceType == PermissionResourceType.PlayMode
                && !await context.PlayModes.IgnoreQueryFilters().AnyAsync(p => p.ClubId == request.ClubId && p.Id == request.ResourceId, cancellationToken))
                throw new EntityNotFoundException(typeof(Bookennis.Domain.Clubs.PlayMode), request.ResourceId!);

            var grantedBy = await context.ClubMembers.IgnoreQueryFilters()
                .Where(m => m.ClubId == request.ClubId && m.UserId == request.UserId)
                .OrderBy(m => m.Id)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // One grant per member, permission and resource
            var existing = await context.PermissionGrants.IgnoreQueryFilters()
                .Where(g => g.ClubId == request.ClubId && g.MemberId == request.MemberId && g.Permission == request.Permission
                         && g.ResourceType == request.ResourceType && g.ResourceId == request.ResourceId)
                .ToListAsync(cancellationToken);
            context.PermissionGrants.RemoveRange(existing);

            var grant = new PermissionGrant(request.ClubId, request.MemberId, request.Permission, request.ResourceType, request.ResourceId, request.Effect, grantedBy, request.Note);
            context.PermissionGrants.Add(grant);
            await context.SaveChangesAsync(cancellationToken);
            return grant.Id;
        }
    }
}

/// <summary>A grant for one court or play mode goes with it (the grant has no foreign key to it).</summary>
public static class PermissionGrantCleanup
{
    public static async Task RemoveForResource(AppDbContext context, int clubId, PermissionResourceType type, int resourceId, CancellationToken cancellationToken)
    {
        var grants = await context.PermissionGrants.IgnoreQueryFilters()
            .Where(g => g.ClubId == clubId && g.ResourceType == type && g.ResourceId == resourceId)
            .ToListAsync(cancellationToken);
        context.PermissionGrants.RemoveRange(grants);
    }
}

[SecurityAudit]
public record DeletePermissionGrant(int ClubId, int GrantId) : ICommand
{
    public class Handler(AppDbContext context) : AsyncRequestHandler<DeletePermissionGrant>
    {
        protected override async Task Handle(DeletePermissionGrant request, CancellationToken cancellationToken)
        {
            var grant = await context.PermissionGrants.IgnoreQueryFilters()
                .SingleOrDefaultAsync(g => g.ClubId == request.ClubId && g.Id == request.GrantId, cancellationToken)
                ?? throw new EntityNotFoundException(typeof(PermissionGrant), request.GrantId);

            context.PermissionGrants.Remove(grant);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
