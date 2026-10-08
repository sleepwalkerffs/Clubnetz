using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.CourtBlockings;

namespace Bookennis.Client.Services.HttpClients.CourtBlockings;

public class CourtBlockingsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : ICourtBlockingsHttpClient
{
    public async Task<HttpResult<GetCourtBlockingOccurrencesResult>> GetCourtBlockingOccurrences(DateOnly dayFrom, DateOnly dayTo, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"?dayFrom={dayFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&dayTo={dayTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", cancellationToken))
            .AsHttpResult<GetCourtBlockingOccurrencesResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetCourtBlockingsResult>> GetCourtBlockings(bool includePast, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"manage?includePast={(includePast ? "true" : "false")}", cancellationToken))
            .AsHttpResult<GetCourtBlockingsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetCourtBlockingConflictsResult>> GetCourtBlockingConflicts(SaveCourtBlockingModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("conflicts", model, jsonOptions, cancellationToken)).AsHttpResult<GetCourtBlockingConflictsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateCourtBlocking(SaveCourtBlockingModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateCourtBlocking(int courtBlockingId, SaveCourtBlockingModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{courtBlockingId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteCourtBlocking(int courtBlockingId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{courtBlockingId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
