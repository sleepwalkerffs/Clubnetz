using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubApiKeys;

namespace Bookennis.Client.Services.Store.ClubApiKeys;

public interface IClubApiKeysStore : ISemaphoreStore
{
    event Action? OnApiKeysChanged;

    /// <summary>The API keys of the club; <c>null</c> until they were loaded.</summary>
    List<ClubApiKeyDto>? ApiKeys { get; }

    Task LoadApiKeys();

    /// <summary>Creates a key. The result carries the key itself, which the server never returns again.</summary>
    Task<HttpResult<CreateClubApiKeyResult>> CreateApiKey(CreateClubApiKeyModel model);

    Task<HttpResult> DeleteApiKey(int clubApiKeyId);
}
