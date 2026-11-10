using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Users;
using Bookennis.Domain.Exceptions;
using Bookennis.Api.Data;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;
using Bookennis.Api.Infrastructure.Audit;

namespace Bookennis.Api.Business.Admin;

[SecurityAudit]
public record DeleteAdminUser(int UserId) : ICommand
{
    public enum ErrorCode
    {
        LastClubAdmin = 0,
    }

    public class Handler(AppDbContext context, IAuditContext auditContext) : IRequestHandler<DeleteAdminUser>
    {
        public async Task<Unit> Handle(DeleteAdminUser request, CancellationToken cancellationToken)
        {
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), request.UserId);

            // A club keeps at least one admin: delete the club or hand over the admin role first
            var memberIds = await context.ClubMembers.IgnoreQueryFilters().Where(m => m.UserId == user.Id).Select(m => m.Id).ToListAsync(cancellationToken);
            if (await MemberRemoval.FindClubLosingItsLastAdmin(context, memberIds, cancellationToken) is { } clubName)
                throw new PreconditionException(ErrorCode.LastClubAdmin, [clubName], $"The user is the last admin of the club {clubName}.");

            await UserDeletion.DeleteUser(context, user, cancellationToken);
            auditContext.PseudonymizeAfterCommand(user.Id);
            return default;
        }
    }
}
