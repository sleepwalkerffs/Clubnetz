using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Memberships;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.Store.Memberships;

public class MembershipsStore(IMembershipsHttpClient httpClient) : SemaphoreStore, IMembershipsStore
{
    public event Action? OnOptionsChanged;
    public event Action? OnCurrentChanged;

    public GetMembershipOptionsResult? Options { get; private set; }
    public GetMembershipOptionsResult? Current { get; private set; }

    public Task LoadOptions(int? seasonId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetMembershipOptions(seasonId, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Options = response.Dto;
            OnOptionsChanged?.Invoke();
        }
    }, nameof(LoadOptions));

    // Its own context: loading the card on My Club must never cancel (or be cancelled by) the membership page
    public Task LoadCurrent() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetMembershipOptions(null, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Current = response.Dto;
            OnCurrentChanged?.Invoke();
        }
    }, nameof(LoadCurrent));

    public async Task<HttpResult<MembershipOrderDto>> CreateOrder(int membershipPlanId, List<int> memberIds)
        => (HttpResult<MembershipOrderDto>)await RunInSavingContextAsync(
            async cancellationToken => await httpClient.CreateMembershipOrder(new CreateMembershipOrderModel { MembershipPlanId = membershipPlanId, MemberIds = memberIds }, cancellationToken),
            nameof(CreateOrder));

    // A read that does not touch the state of the store: the page polls it while it waits for the payment
    public Task<HttpResult<MembershipOrderDto>> GetOrder(int orderId) => httpClient.GetMembershipOrder(orderId);

    public async Task<HttpResult<StartMembershipCheckoutResult>> StartCheckout(int orderId)
        => (HttpResult<StartMembershipCheckoutResult>)await RunInSavingContextAsync(async cancellationToken => await httpClient.StartMembershipCheckout(orderId, cancellationToken), nameof(StartCheckout));

    public async Task<HttpResult<MembershipOrderDto>> ConfirmOrder(int orderId)
        => (HttpResult<MembershipOrderDto>)await RunInSavingContextAsync(async cancellationToken => await httpClient.ConfirmMembershipOrder(orderId, cancellationToken), nameof(ConfirmOrder));

    public async Task<HttpResult<MembershipOrderDto>> CancelOrder(int orderId)
        => (HttpResult<MembershipOrderDto>)await RunInSavingContextAsync(async cancellationToken => await httpClient.CancelMembershipOrder(orderId, cancellationToken), nameof(CancelOrder));
}
