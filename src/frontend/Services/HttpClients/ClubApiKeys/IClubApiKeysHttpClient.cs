using Bookennis.Shared.Controller.ClubApiKeys;

namespace Bookennis.Client.Services.HttpClients.ClubApiKeys;

public interface IClubApiKeysHttpClient
{
    public Task<HttpResult<GetClubApiKeysResult>> GetClubApiKeys(CancellationToken cancellationToken = default);
    public Task<HttpResult<CreateClubApiKeyResult>> CreateClubApiKey(CreateClubApiKeyModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteClubApiKey(int clubApiKeyId, CancellationToken cancellationToken = default);
}
