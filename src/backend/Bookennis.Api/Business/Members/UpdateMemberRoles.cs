using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

[SecurityAudit]
public record UpdateMemberRoles(int MemberId, MemberRole[] Roles) : ICommand
{
    public enum ErrorCode
    {
        /// <summary>The club would be left without an admin.</summary>
        LastAdminCannotBeRemoved = 0,
    }

    public class Handler(AppDbContext context) : AsyncRequestHandler<UpdateMemberRoles>
    {
        protected override async Task Handle(UpdateMemberRoles request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers.SingleRequiredAsync(m => m.Id == request.MemberId, cancellationToken);

            // The last admin keeps the admin role: nobody could manage the club (and its permissions) any more.
            // The lock makes two admins who take the role from each other at the same time see each other.
            if (member.UserRoles.Contains(MemberRole.Admin) && !request.Roles.Contains(MemberRole.Admin))
            {
                await context.LockClubAdmins(member.ClubId, cancellationToken);

                var otherAdminExists = await context.ClubMembers.IgnoreQueryFilters()
                    .AnyAsync(m => m.ClubId == member.ClubId && m.Id != member.Id && m.UserRoles.Contains(MemberRole.Admin), cancellationToken);
                if (!otherAdminExists)
                    throw new PreconditionException(ErrorCode.LastAdminCannotBeRemoved, "The last admin of the club can't give up the admin role.");
            }

            member.UpdateRoles(request.Roles);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

public static class ClubAdminLock
{
    private const long AdminLockNamespace = 0x41444D494EL << 32; // "ADMIN"

    /// <summary>Advisory lock per club until the transaction ends: whoever takes admin rights away from a member goes one after the other.</summary>
    public static Task LockClubAdmins(this AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        var key = AdminLockNamespace + clubId;
        return context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
}
