using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.Store.Memberships;

/// <summary>The membership of the signed-in member: the plans they can choose and their orders.</summary>
public interface IMembershipsStore : ISemaphoreStore
{
    event Action? OnOptionsChanged;
    event Action? OnCurrentChanged;

    /// <summary>What the membership page shows for the season it has open; <c>null</c> until it was loaded.</summary>
    GetMembershipOptionsResult? Options { get; }

    /// <summary>
    /// The running (else the next) season, for the card on My Club. Kept apart from <see cref="Options"/>,
    /// so the membership page with another season open can't overwrite it.
    /// </summary>
    GetMembershipOptionsResult? Current { get; }

    Task LoadOptions(int? seasonId);
    Task LoadCurrent();

    Task<HttpResult<MembershipOrderDto>> CreateOrder(int membershipPlanId, List<int> memberIds);
    Task<HttpResult<MembershipOrderDto>> GetOrder(int orderId);

    /// <summary>The address of the payment page the browser has to be sent to.</summary>
    Task<HttpResult<StartMembershipCheckoutResult>> StartCheckout(int orderId);

    Task<HttpResult<MembershipOrderDto>> ConfirmOrder(int orderId);
    Task<HttpResult<MembershipOrderDto>> CancelOrder(int orderId);
}
