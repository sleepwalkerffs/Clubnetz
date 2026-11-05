using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.PlayRequests;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.PlayRequests;

namespace Bookennis.Client.Services.Store.PlayRequests;

public class PlayRequestsStore(IPlayRequestsHttpClient httpClient, IPlayerGroupsHttpClient groupsHttpClient) : SemaphoreStore, IPlayRequestsStore
{
    public event Action? OnPlayRequestsChanged;
    public event Action? OnGroupsChanged;

    public List<PlayRequestDto>? Requests { get; private set; }
    public List<PlayRequestCandidateDto> Candidates { get; private set; } = [];
    public List<PlayerGroupDto> Groups { get; private set; } = [];

    public Task LoadRequests() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetPlayRequests(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Requests = response.Dto;
            OnPlayRequestsChanged?.Invoke();
        }
    }, nameof(LoadRequests));

    public Task LoadCandidates() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetCandidates(cancellationToken);
        if (response is { Success: true, Dto: not null })
            Candidates = response.Dto;
    }, nameof(LoadCandidates));

    public Task LoadGroups() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await groupsHttpClient.GetPlayerGroups(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Groups = response.Dto;
            OnGroupsChanged?.Invoke();
        }
    }, nameof(LoadGroups));

    public async Task<HttpResult<int>> Create(CreatePlayRequestModel model)
        => (HttpResult<int>)await RunInSavingContextAsync(async cancellationToken => await httpClient.CreatePlayRequest(model, cancellationToken), nameof(Create));

    public async Task<HttpResult<PlayRequestAcceptResult>> Accept(int playRequestId)
        => (HttpResult<PlayRequestAcceptResult>)await RunInSavingContextAsync(async cancellationToken => await httpClient.AcceptPlayRequest(playRequestId, cancellationToken), nameof(Accept));

    public Task<HttpResult> Decline(int playRequestId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.DeclinePlayRequest(playRequestId, cancellationToken), nameof(Decline));

    public Task<HttpResult> Cancel(int playRequestId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.CancelPlayRequest(playRequestId, cancellationToken), nameof(Cancel));

    public async Task<HttpResult<PlayerGroupDto>> CreateGroup(SavePlayerGroupModel model)
        => (HttpResult<PlayerGroupDto>)await RunInSavingContextAsync(async cancellationToken => await groupsHttpClient.CreatePlayerGroup(model, cancellationToken), nameof(CreateGroup));

    public async Task<HttpResult<PlayerGroupDto>> UpdateGroup(int groupId, SavePlayerGroupModel model)
        => (HttpResult<PlayerGroupDto>)await RunInSavingContextAsync(async cancellationToken => await groupsHttpClient.UpdatePlayerGroup(groupId, model, cancellationToken), nameof(UpdateGroup));

    public Task<HttpResult> DeleteGroup(int groupId)
        => RunInSavingContextAsync(async cancellationToken => await groupsHttpClient.DeletePlayerGroup(groupId, cancellationToken), nameof(DeleteGroup));
}
