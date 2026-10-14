using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Memberships;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Memberships;

public class PaymentAccountStore(IPaymentAccountHttpClient httpClient) : SemaphoreStore, IPaymentAccountStore
{
    public event Action? OnAccountChanged;

    public PaymentAccountDto? Account { get; private set; }

    public Task LoadAccount() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetPaymentAccount(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Account = response.Dto;
            OnAccountChanged?.Invoke();
        }
    }, nameof(LoadAccount));

    public async Task<HttpResult<ConnectPaymentAccountResult>> Connect(Country country)
        => (HttpResult<ConnectPaymentAccountResult>)await RunInSavingContextAsync(
            async cancellationToken => await httpClient.ConnectPaymentAccount(new ConnectPaymentAccountModel { Country = country }, cancellationToken),
            nameof(Connect));

    public Task<HttpResult> Refresh() => RunInSavingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.RefreshPaymentAccount(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Account = response.Dto;
            OnAccountChanged?.Invoke();
        }

        return response;
    }, nameof(Refresh));

    public Task<HttpResult> Disconnect() => RunInSavingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.DisconnectPaymentAccount(cancellationToken);
        if (response.Success && Account is not null)
        {
            Account = Account with { IsConnected = false, ChargesEnabled = false, PayoutsEnabled = false, DetailsSubmitted = false, ConnectedAt = null };
            OnAccountChanged?.Invoke();
        }

        return response;
    }, nameof(Disconnect));
}
