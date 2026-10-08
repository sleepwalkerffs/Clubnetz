using Bookennis.Shared.Controller.Guests;

namespace Bookennis.Client.Services.HttpClients.Guests;

public interface IGuestHttpClient
{
    Task<HttpResult<GetGuestCardResult>> GetGuestCards(CancellationToken cancellationToken = default);
    Task<HttpResult<CreateGuestCardResult>> CreateGuestCard(CreateGuestCardRequest request, CancellationToken cancellationToken = default);
    Task<HttpResult> DeleteGuestCard(int id, CancellationToken cancellationToken = default);
}
