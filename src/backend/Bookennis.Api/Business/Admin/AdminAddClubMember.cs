using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminAddClubMember(int ClubId, int UserId, MemberRole[] Roles) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminAddClubMember>
    {
        public async Task<Unit> Handle(AdminAddClubMember request, CancellationToken cancellationToken)
        {
            var userExists = await context.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
            if (!userExists)
                throw new EntityNotFoundException(typeof(User), request.UserId);

            var clubExists = await context.Clubs.AnyAsync(c => c.Id == request.ClubId, cancellationToken);
            if (!clubExists)
                throw new EntityNotFoundException(typeof(Club), request.ClubId);

            var alreadyMember = await context.ClubMembers.AnyAsync(m => m.UserId == request.UserId && m.ClubId == request.ClubId, cancellationToken);
            if (alreadyMember)
                return default;

            var member = new ClubMember(request.UserId, request.ClubId, request.Roles);
            context.ClubMembers.Add(member);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
