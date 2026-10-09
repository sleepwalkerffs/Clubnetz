using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubApiKeys;

/// <summary>Revokes an API key: it is rejected from now on.</summary>
public record DeleteClubApiKey(int ClubId, int ClubApiKeyId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteClubApiKey>
    {
        public async Task<Unit> Handle(DeleteClubApiKey request, CancellationToken cancellationToken)
        {
            var key = await context.ClubApiKeys
                .SingleRequiredAsync(k => k.Id == request.ClubApiKeyId && k.ClubId == request.ClubId, cancellationToken);

            context.ClubApiKeys.Remove(key);

            await context.SaveChangesAsync(cancellationToken);

            return default;
        }
    }
}
