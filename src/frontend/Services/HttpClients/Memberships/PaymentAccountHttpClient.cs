using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public class PaymentAccountHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IPaymentAccountHttpClient
{
    public async Task<HttpResult<PaymentConfigurationResult>> GetPaymentConfiguration(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("configuration", cancellationToken)).AsHttpResult<PaymentConfigurationResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<PaymentAccountDto>> GetPaymentAccount(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("account", cancellationToken)).AsHttpResult<PaymentAccountDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ConnectPaymentAccountResult>> ConnectPaymentAccount(ConnectPaymentAccountModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("account/connect", model, jsonOptions, cancellationToken)).AsHttpResult<ConnectPaymentAccountResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<PaymentAccountDto>> RefreshPaymentAccount(CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync("account/refresh", null, cancellationToken)).AsHttpResult<PaymentAccountDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DisconnectPaymentAccount(CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync("account", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
