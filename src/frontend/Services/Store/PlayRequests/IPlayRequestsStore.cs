using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.PlayRequests;

namespace Bookennis.Client.Services.Store.PlayRequests;

public interface IPlayRequestsStore : ISemaphoreStore
{
    event Action? OnPlayRequestsChanged;
    event Action? OnGroupsChanged;

    /// <summary>The requests of the signed-in member (their own and those they were asked for); <c>null</c> until loaded.</summary>
    List<PlayRequestDto>? Requests { get; }

    /// <summary>The members who can be asked.</summary>
    List<PlayRequestCandidateDto> Candidates { get; }

    /// <summary>The saved groups of the signed-in member.</summary>
    List<PlayerGroupDto> Groups { get; }

    Task LoadRequests();
    Task LoadCandidates();
    Task LoadGroups();
    Task<HttpResult<int>> Create(CreatePlayRequestModel model);
    Task<HttpResult<PlayRequestAcceptResult>> Accept(int playRequestId);
    Task<HttpResult> Decline(int playRequestId);
    Task<HttpResult> Cancel(int playRequestId);
    Task<HttpResult<PlayerGroupDto>> CreateGroup(SavePlayerGroupModel model);
    Task<HttpResult<PlayerGroupDto>> UpdateGroup(int groupId, SavePlayerGroupModel model);
    Task<HttpResult> DeleteGroup(int groupId);
}
