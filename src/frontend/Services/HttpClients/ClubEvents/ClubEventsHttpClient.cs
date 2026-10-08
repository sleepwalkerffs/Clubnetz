using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Client.Services.HttpClients.ClubEvents;

public class ClubEventsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IClubEventsHttpClient
{
    public async Task<HttpResult<GetClubEventsResult>> GetClubEvents(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"?from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", cancellationToken))
            .AsHttpResult<GetClubEventsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubEventDto>> GetClubEvent(int clubEventId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{clubEventId}", cancellationToken)).AsHttpResult<ClubEventDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateClubEvent(SaveClubEventModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubEventDto>> UpdateClubEvent(int clubEventId, SaveClubEventModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{clubEventId}", model, jsonOptions, cancellationToken)).AsHttpResult<ClubEventDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<PreviewClubEventDescriptionResult>> PreviewClubEventDescription(PreviewClubEventDescriptionModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("preview", model, jsonOptions, cancellationToken)).AsHttpResult<PreviewClubEventDescriptionResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteClubEvent(int clubEventId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{clubEventId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubEventDto>> RegisterForClubEvent(int clubEventId, RegisterForClubEventModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{clubEventId}/registration", model, jsonOptions, cancellationToken)).AsHttpResult<ClubEventDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubEventDto>> UnregisterFromClubEvent(int clubEventId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{clubEventId}/registration", cancellationToken)).AsHttpResult<ClubEventDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<ClubEventDto>> RemoveClubEventRegistration(int clubEventId, int registrationId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{clubEventId}/registrations/{registrationId}", cancellationToken)).AsHttpResult<ClubEventDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<FileDownload>> ExportClubEventParticipants(int clubEventId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{clubEventId}/export", cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);
}
