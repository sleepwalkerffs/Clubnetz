using Bookennis.Api.Data;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminUpdateFamilyMember(int ClubId, int MemberId, string FirstName, string LastName, DateOnly Birthday, Gender Gender) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminUpdateFamilyMember>
    {
        public async Task<Unit> Handle(AdminUpdateFamilyMember request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers.SingleOrDefaultAsync(m => m.Id == request.MemberId && m.ClubId == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.Members.ClubMember), request.MemberId);

            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == member.UserId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(User), member.UserId);

            user.Update(request.FirstName, request.LastName, request.Birthday, request.Gender, user.Language, user.Street, user.City, user.ZipCode, user.Country);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
