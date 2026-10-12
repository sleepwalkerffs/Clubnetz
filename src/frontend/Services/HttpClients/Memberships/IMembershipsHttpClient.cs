using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public interface IMembershipsHttpClient
{
    public Task<HttpResult<GetMembershipOptionsResult>> GetMembershipOptions(int? seasonId, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> CreateMembershipOrder(CreateMembershipOrderModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> GetMembershipOrder(int orderId, CancellationToken cancellationToken = default);
    public Task<HttpResult<StartMembershipCheckoutResult>> StartMembershipCheckout(int orderId, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> ConfirmMembershipOrder(int orderId, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> CancelMembershipOrder(int orderId, CancellationToken cancellationToken = default);
}
