using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public interface IPaymentAccountHttpClient
{
    public Task<HttpResult<PaymentConfigurationResult>> GetPaymentConfiguration(CancellationToken cancellationToken = default);
    public Task<HttpResult<PaymentAccountDto>> GetPaymentAccount(CancellationToken cancellationToken = default);
    public Task<HttpResult<ConnectPaymentAccountResult>> ConnectPaymentAccount(ConnectPaymentAccountModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<PaymentAccountDto>> RefreshPaymentAccount(CancellationToken cancellationToken = default);
    public Task<HttpResult> DisconnectPaymentAccount(CancellationToken cancellationToken = default);
}
