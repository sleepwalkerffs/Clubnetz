using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.ClubApiKeys;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubApiKeys;

/// <summary>Creates an API key that acts as the user in the club. The key is only returned by this command.</summary>
public record CreateClubApiKey(int ClubId, int UserId, string? Name, bool IsReadOnly, int? ExpiresInDays) : ICommand<CreateClubApiKeyResult>
{
    public enum ErrorCode
    {
        ApiKeyNameRequired = 0,
        ApiKeyNameTooLong = 1,
        ApiKeyInvalidExpiry = 2,
        ApiKeyCreatorNotClubAdmin = 3,
        ApiKeyLimitReached = 4
    }

    public class Handler(AppDbContext context) : IRequestHandler<CreateClubApiKey, CreateClubApiKeyResult>
    {
        public async Task<CreateClubApiKeyResult> Handle(CreateClubApiKey request, CancellationToken cancellationToken)
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                throw new PreconditionException(ErrorCode.ApiKeyNameRequired, "The API key needs a name.");
            if (name.Length > ClubApiKey.NameMaxLength)
                throw new PreconditionException(ErrorCode.ApiKeyNameTooLong, [Format(ClubApiKey.NameMaxLength)], $"The name must not exceed {ClubApiKey.NameMaxLength} characters.");
            if (request.ExpiresInDays is < 1 or > ClubApiKey.MaxExpiryInDays)
                throw new PreconditionException(ErrorCode.ApiKeyInvalidExpiry, [Format(ClubApiKey.MaxExpiryInDays)], $"The key must expire in 1 to {ClubApiKey.MaxExpiryInDays} days.");

            // The key acts as its creator, so an administrator in support mode (no member of the club) can't create one
            if (!await context.ClubAdmins(request.ClubId).AnyAsync(m => m.UserId == request.UserId, cancellationToken))
                throw new PreconditionException(ErrorCode.ApiKeyCreatorNotClubAdmin, "Only an admin of the club can create an API key.");

            if (await context.ClubApiKeys.CountAsync(k => k.ClubId == request.ClubId, cancellationToken) >= ClubApiKey.MaxKeysPerClub)
                throw new PreconditionException(ErrorCode.ApiKeyLimitReached, [Format(ClubApiKey.MaxKeysPerClub)], $"A club can have at most {ClubApiKey.MaxKeysPerClub} API keys.");

            DateTimeOffset? expiresAt = request.ExpiresInDays is { } days ? DateTimeOffset.UtcNow.AddDays(days) : null;
            var (key, token) = ClubApiKey.Create(request.ClubId, request.UserId, name, request.IsReadOnly, expiresAt);

            context.ClubApiKeys.Add(key);
            await context.SaveChangesAsync(cancellationToken);

            return new CreateClubApiKeyResult { Id = key.Id, Token = token };
        }

        private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
