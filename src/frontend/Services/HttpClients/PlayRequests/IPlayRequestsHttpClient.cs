using Bookennis.Shared.Controller.PlayRequests;

namespace Bookennis.Client.Services.HttpClients.PlayRequests;

public interface IPlayRequestsHttpClient
{
    Task<HttpResult<List<PlayRequestDto>>> GetPlayRequests(CancellationToken cancellationToken = default);
    Task<HttpResult<List<PlayRequestCandidateDto>>> GetCandidates(CancellationToken cancellationToken = default);
    Task<HttpResult<int>> CreatePlayRequest(CreatePlayRequestModel model, CancellationToken cancellationToken = default);
    Task<HttpResult<PlayRequestAcceptResult>> AcceptPlayRequest(int playRequestId, CancellationToken cancellationToken = default);
    Task<HttpResult> DeclinePlayRequest(int playRequestId, CancellationToken cancellationToken = default);
    Task<HttpResult> CancelPlayRequest(int playRequestId, CancellationToken cancellationToken = default);
}

public interface IPlayerGroupsHttpClient
{
    Task<HttpResult<List<PlayerGroupDto>>> GetPlayerGroups(CancellationToken cancellationToken = default);
    Task<HttpResult<PlayerGroupDto>> CreatePlayerGroup(SavePlayerGroupModel model, CancellationToken cancellationToken = default);
    Task<HttpResult<PlayerGroupDto>> UpdatePlayerGroup(int groupId, SavePlayerGroupModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> DeletePlayerGroup(int groupId, CancellationToken cancellationToken = default);
}
