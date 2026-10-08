using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Guests;

namespace Bookennis.Client.Services.HttpClients.Guests;

public class GuestHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IGuestHttpClient
{
    public async Task<HttpResult<CreateGuestCardResult>> CreateGuestCard(CreateGuestCardRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync((string?)null, request, jsonOptions, cancellationToken)).AsHttpResult<CreateGuestCardResult>(jsonOptions, cancellationToken);
    public async Task<HttpResult<GetGuestCardResult>> GetGuestCards(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync((string?)null, cancellationToken)).AsHttpResult<GetGuestCardResult>(jsonOptions, cancellationToken);
    public async Task<HttpResult> DeleteGuestCard(int id, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync(id.ToString(), cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
