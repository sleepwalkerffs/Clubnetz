using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.Store.Memberships;

/// <summary>The treasurer's view of the membership fees of a season.</summary>
public interface IMembershipPaymentsStore : ISemaphoreStore
{
    event Action? OnPaymentsChanged;

    /// <summary>Members and orders of the season that was loaded last; <c>null</c> until they were loaded.</summary>
    GetMembershipPaymentsResult? Payments { get; }

    GetMembershipPaymentsSummaryResult? Summary { get; }

    /// <summary>Loads the list and the summary of a season.</summary>
    Task Load(int seasonId);

    Task<HttpResult> RecordPayment(RecordMembershipPaymentModel model);
    /// <summary>Confirms an order; with <paramref name="memberIds"/> it covers these members from now on.</summary>
    Task<HttpResult> Approve(int orderId, IReadOnlyCollection<int>? memberIds = null);
    Task<HttpResult> Reject(int orderId);
    Task<HttpResult> MarkPaid(int orderId, MarkMembershipOrderPaidModel model);
    Task<HttpResult> Cancel(int orderId);
    Task<HttpResult<FileDownload>> Export(int seasonId);
}
