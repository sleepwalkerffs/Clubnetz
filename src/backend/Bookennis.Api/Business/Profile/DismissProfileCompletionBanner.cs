using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

public record DismissProfileCompletionBanner : ICommand
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<DismissProfileCompletionBanner>
    {
        public async Task<Unit> Handle(DismissProfileCompletionBanner request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            var user = await context.Users.SingleRequiredAsync(x => x.Id == userId, cancellationToken);
            user.DismissProfileCompletionBanner();
            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
