using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public interface IMembershipPaymentsHttpClient
{
    public Task<HttpResult<GetMembershipPaymentsResult>> GetMembershipPayments(int seasonId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetMembershipPaymentsSummaryResult>> GetMembershipPaymentsSummary(int seasonId, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> RecordMembershipPayment(RecordMembershipPaymentModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> ApproveMembershipOrder(int orderId, ApproveMembershipOrderModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> RejectMembershipOrder(int orderId, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> MarkMembershipOrderPaid(int orderId, MarkMembershipOrderPaidModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<MembershipOrderDto>> CancelMembershipOrder(int orderId, bool removeEnrollments, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> ExportMembershipPayments(int seasonId, CancellationToken cancellationToken = default);
}
