using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

public record BecomeClubMember([Required] int ClubId) : ICommand
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<BecomeClubMember>
    {
        public async Task<Unit> Handle(BecomeClubMember request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var id))
                throw new AuthorizationFailedException();

            var user = await context.Users.SingleRequiredAsync(x => x.Id == id, cancellationToken);
            if (await context.Set<Member>().AnyAsync(member => member.UserId == user.Id && member.ClubId == request.ClubId, cancellationToken))
                return default;

            var member = new ClubMember(user.Id, request.ClubId, [MemberRole.User]);
            context.Add(member);
            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
