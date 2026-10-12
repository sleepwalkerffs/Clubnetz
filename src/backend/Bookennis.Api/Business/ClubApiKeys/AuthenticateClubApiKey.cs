using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.ApiKeys;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubApiKeys;

/// <summary>
/// Checks an API key for a request to the club and returns who it acts as, or <c>null</c> when it must be rejected:
/// unknown, expired, a key of another club, or its creator is no longer an admin of the club.
/// </summary>
public record AuthenticateClubApiKey(string Token, int ClubId) : ICommand<AuthenticatedClubApiKey?>
{
    /// <summary>The last use is only written this often, so reading with a key does not write on every request.</summary>
    public static readonly TimeSpan LastUsedPrecision = TimeSpan.FromMinutes(5);

    public class Handler(AppDbContext context) : IRequestHandler<AuthenticateClubApiKey, AuthenticatedClubApiKey?>
    {
        public async Task<AuthenticatedClubApiKey?> Handle(AuthenticateClubApiKey request, CancellationToken cancellationToken)
        {
            if (!ClubApiKey.IsToken(request.Token))
                return null;

            var now = DateTimeOffset.UtcNow;
            var hash = ClubApiKey.Hash(request.Token);

            var key = await context.ClubApiKeys
                .IgnoreQueryFilters()
                .Where(k => k.KeyHash == hash && k.ClubId == request.ClubId)
                .Select(k => new
                {
                    k.Id,
                    k.CreatedByUserId,
                    k.IsReadOnly,
                    k.ExpiresAt,
                    k.LastUsedAt,
                    CreatorIsAdmin = context.ClubAdmins(request.ClubId).Any(m => m.UserId == k.CreatedByUserId),
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (key is null || !key.CreatorIsAdmin || (key.ExpiresAt is not null && key.ExpiresAt <= now))
                return null;

            if (key.LastUsedAt is null || now - key.LastUsedAt > LastUsedPrecision)
            {
                await context.ClubApiKeys
                    .IgnoreQueryFilters()
                    .Where(k => k.Id == key.Id)
                    .ExecuteUpdateAsync(k => k.SetProperty(x => x.LastUsedAt, now), cancellationToken);
            }

            return new AuthenticatedClubApiKey(key.Id, request.ClubId, key.CreatedByUserId, key.IsReadOnly);
        }
    }
}

public record AuthenticatedClubApiKey(int ClubApiKeyId, int ClubId, int UserId, bool IsReadOnly);
