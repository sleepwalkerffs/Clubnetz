using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminRemoveClubMember(int ClubId, int MemberId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminRemoveClubMember>
    {
        public async Task<Unit> Handle(AdminRemoveClubMember request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers.SingleOrDefaultAsync(m => m.Id == request.MemberId && m.ClubId == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(ClubMember), request.MemberId);

            context.ClubMembers.Remove(member);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
