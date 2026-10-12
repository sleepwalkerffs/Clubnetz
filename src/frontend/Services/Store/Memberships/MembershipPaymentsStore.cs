using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Memberships;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.Store.Memberships;

public class MembershipPaymentsStore(IMembershipPaymentsHttpClient httpClient) : SemaphoreStore, IMembershipPaymentsStore
{
    public event Action? OnPaymentsChanged;

    public GetMembershipPaymentsResult? Payments { get; private set; }
    public GetMembershipPaymentsSummaryResult? Summary { get; private set; }

    public Task Load(int seasonId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Payments?.SeasonId != seasonId)
        {
            Payments = null;
            Summary = null;
            OnPaymentsChanged?.Invoke();
        }

        var paymentsTask = httpClient.GetMembershipPayments(seasonId, cancellationToken);
        var summaryTask = httpClient.GetMembershipPaymentsSummary(seasonId, cancellationToken);
        var payments = await paymentsTask;
        var summary = await summaryTask;

        if (payments is { Success: true, Dto: not null } && summary is { Success: true, Dto: not null })
        {
            Payments = payments.Dto;
            Summary = summary.Dto;
            OnPaymentsChanged?.Invoke();
        }
    }, nameof(Load));

    public Task<HttpResult> RecordPayment(RecordMembershipPaymentModel model)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.RecordMembershipPayment(model, cancellationToken), nameof(RecordPayment));

    public Task<HttpResult> Approve(int orderId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.ApproveMembershipOrder(orderId, cancellationToken), nameof(Approve));

    public Task<HttpResult> Reject(int orderId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.RejectMembershipOrder(orderId, cancellationToken), nameof(Reject));

    public Task<HttpResult> MarkPaid(int orderId, MarkMembershipOrderPaidModel model)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.MarkMembershipOrderPaid(orderId, model, cancellationToken), nameof(MarkPaid));

    public Task<HttpResult> Cancel(int orderId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.CancelMembershipOrder(orderId, cancellationToken), nameof(Cancel));

    public async Task<HttpResult<FileDownload>> Export(int seasonId)
        => (HttpResult<FileDownload>)await RunInSavingContextAsync(async cancellationToken => await httpClient.ExportMembershipPayments(seasonId, cancellationToken), nameof(Export));
}
