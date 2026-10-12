using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.ClubApiKeys;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubApiKeys;

namespace Bookennis.Client.Services.Store.ClubApiKeys;

public class ClubApiKeysStore(IClubApiKeysHttpClient httpClient) : SemaphoreStore, IClubApiKeysStore
{
    public event Action? OnApiKeysChanged;

    public List<ClubApiKeyDto>? ApiKeys { get; private set; }

    public Task LoadApiKeys() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetClubApiKeys(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            ApiKeys = response.Dto.ApiKeys;
            OnApiKeysChanged?.Invoke();
        }
    }, nameof(LoadApiKeys));

    public async Task<HttpResult<CreateClubApiKeyResult>> CreateApiKey(CreateClubApiKeyModel model)
        => (HttpResult<CreateClubApiKeyResult>)await RunInSavingContextAsync(async cancellationToken => await httpClient.CreateClubApiKey(model, cancellationToken), nameof(CreateApiKey));

    public Task<HttpResult> DeleteApiKey(int clubApiKeyId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.DeleteClubApiKey(clubApiKeyId, cancellationToken), nameof(DeleteApiKey));
}
