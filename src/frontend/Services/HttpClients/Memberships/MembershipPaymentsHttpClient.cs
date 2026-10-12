using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public class MembershipPaymentsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IMembershipPaymentsHttpClient
{
    public async Task<HttpResult<GetMembershipPaymentsResult>> GetMembershipPayments(int seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"?seasonId={seasonId}", cancellationToken)).AsHttpResult<GetMembershipPaymentsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetMembershipPaymentsSummaryResult>> GetMembershipPaymentsSummary(int seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"summary?seasonId={seasonId}", cancellationToken)).AsHttpResult<GetMembershipPaymentsSummaryResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> RecordMembershipPayment(RecordMembershipPaymentModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> ApproveMembershipOrder(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"{orderId}/approve", null, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> RejectMembershipOrder(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"{orderId}/reject", null, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> MarkMembershipOrderPaid(int orderId, MarkMembershipOrderPaidModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"{orderId}/mark-paid", model, jsonOptions, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembershipOrderDto>> CancelMembershipOrder(int orderId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"{orderId}/cancel", null, cancellationToken)).AsHttpResult<MembershipOrderDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FileDownload>> ExportMembershipPayments(int seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"export?seasonId={seasonId}", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);
}
