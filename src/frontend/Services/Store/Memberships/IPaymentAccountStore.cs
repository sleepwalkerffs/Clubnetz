using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Memberships;

/// <summary>Online payment of the club: its Stripe account, connected and disconnected by an admin.</summary>
public interface IPaymentAccountStore : ISemaphoreStore
{
    event Action? OnAccountChanged;

    /// <summary>The Stripe account of the club; <c>null</c> until it was loaded.</summary>
    PaymentAccountDto? Account { get; }

    Task LoadAccount();

    /// <summary>Creates the account at Stripe (once) and returns the address of the onboarding form of Stripe.</summary>
    Task<HttpResult<ConnectPaymentAccountResult>> Connect(Country country);

    /// <summary>Asks Stripe for the status of the account.</summary>
    Task<HttpResult> Refresh();

    Task<HttpResult> Disconnect();
}
