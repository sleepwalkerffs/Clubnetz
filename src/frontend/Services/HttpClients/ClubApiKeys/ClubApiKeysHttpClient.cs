using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.ClubApiKeys;

namespace Bookennis.Client.Services.HttpClients.ClubApiKeys;

public class ClubApiKeysHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IClubApiKeysHttpClient
{
    public async Task<HttpResult<GetClubApiKeysResult>> GetClubApiKeys(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<GetClubApiKeysResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CreateClubApiKeyResult>> CreateClubApiKey(CreateClubApiKeyModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<CreateClubApiKeyResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteClubApiKey(int clubApiKeyId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{clubApiKeyId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
