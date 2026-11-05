using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.PlayRequests;

namespace Bookennis.Client.Services.HttpClients.PlayRequests;

public class PlayRequestsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IPlayRequestsHttpClient
{
    public async Task<HttpResult<List<PlayRequestDto>>> GetPlayRequests(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<List<PlayRequestDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<PlayRequestCandidateDto>>> GetCandidates(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("candidates", cancellationToken)).AsHttpResult<List<PlayRequestCandidateDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreatePlayRequest(CreatePlayRequestModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult<PlayRequestAcceptResult>> AcceptPlayRequest(int playRequestId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"{playRequestId}/accept", null, cancellationToken)).AsHttpResult<PlayRequestAcceptResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeclinePlayRequest(int playRequestId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"{playRequestId}/decline", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> CancelPlayRequest(int playRequestId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{playRequestId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}

public class PlayerGroupsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IPlayerGroupsHttpClient
{
    public async Task<HttpResult<List<PlayerGroupDto>>> GetPlayerGroups(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<List<PlayerGroupDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<PlayerGroupDto>> CreatePlayerGroup(SavePlayerGroupModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<PlayerGroupDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<PlayerGroupDto>> UpdatePlayerGroup(int groupId, SavePlayerGroupModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{groupId}", model, jsonOptions, cancellationToken)).AsHttpResult<PlayerGroupDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeletePlayerGroup(int groupId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{groupId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
