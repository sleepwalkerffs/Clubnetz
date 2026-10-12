using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public class MembershipsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IMembershipsHttpClient
{
    public async Task<HttpResult<GetMembershipOptionsResult>> GetMembershipOptions(int? seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(seasonId is null ? "options" : $"options?seasonId={seasonId}", cancellationToken))
            .AsHttpResult<GetMembershipOptionsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> CreateMembershipOrder(CreateMembershipOrderModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("orders", model, jsonOptions, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> GetMembershipOrder(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"orders/{orderId}", cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<StartMembershipCheckoutResult>> StartMembershipCheckout(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"orders/{orderId}/checkout", null, cancellationToken)).AsHttpResult<StartMembershipCheckoutResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> ConfirmMembershipOrder(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"orders/{orderId}/confirm", null, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> CancelMembershipOrder(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"orders/{orderId}", cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);
}
