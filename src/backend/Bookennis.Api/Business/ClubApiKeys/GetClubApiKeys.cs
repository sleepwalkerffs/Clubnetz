using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubApiKeys;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubApiKeys;

/// <summary>The API keys of the club, newest first.</summary>
public record GetClubApiKeys(int ClubId) : IQuery<GetClubApiKeysResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubApiKeys, GetClubApiKeysResult>
    {
        public async Task<GetClubApiKeysResult> Handle(GetClubApiKeys request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;

            var keys = await (from key in context.ClubApiKeys
                              join user in context.Users on key.CreatedByUserId equals user.Id
                              where key.ClubId == request.ClubId
                              orderby key.Metadata.Created descending, key.Id descending
                              select new
                              {
                                  key.Id,
                                  key.Name,
                                  key.KeyPrefix,
                                  key.IsReadOnly,
                                  user.FirstName,
                                  user.LastName,
                                  key.Metadata.Created,
                                  key.ExpiresAt,
                                  key.LastUsedAt,
                                  CreatorIsAdmin = context.ClubAdmins(request.ClubId).Any(m => m.UserId == key.CreatedByUserId),
                              }).ToListAsync(cancellationToken);

            return new GetClubApiKeysResult
            {
                ApiKeys = keys.ConvertAll(key => new ClubApiKeyDto
                {
                    Id = key.Id,
                    Name = key.Name,
                    KeyPrefix = key.KeyPrefix,
                    IsReadOnly = key.IsReadOnly,
                    CreatedByName = $"{key.FirstName} {key.LastName}".Trim(),
                    CreatedAt = key.Created,
                    ExpiresAt = key.ExpiresAt,
                    LastUsedAt = key.LastUsedAt,
                    Status = key.ExpiresAt is not null && key.ExpiresAt <= now
                        ? ClubApiKeyStatus.Expired
                        : key.CreatorIsAdmin ? ClubApiKeyStatus.Active : ClubApiKeyStatus.CreatorNotAdmin,
                }),
            };
        }
    }
}
