using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Club;

namespace Bookennis.Client.Services.HttpClients.Clubs;

public class ClubsHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IClubsHttpClient
{
    public async Task<HttpResult<ClubInformationResult>> GetClubInformation(int clubId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync((string?)null, cancellationToken)).AsHttpResult<ClubInformationResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateClubInformation(int clubId, ClubInformationModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync((string?)null, model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetPlayModesResult>> GetPlayModes(int clubId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("PlayModes", cancellationToken)).AsHttpResult<GetPlayModesResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> AddPlayMode(int clubId, PlayModeRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("PlayModes", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdatePlayMode(int clubId, int playModeId, PlayModeRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"PlayModes/{playModeId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeletePlayMode(int clubId, int playModeId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"PlayModes/{playModeId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetCourtsResult>> GetCourts(int clubId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("Courts", cancellationToken)).AsHttpResult<GetCourtsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetPlayersResult>> GetPlayers(int clubId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("Players", cancellationToken)).AsHttpResult<GetPlayersResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateCourt(int courtId, UpdateCourtRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PatchAsJsonAsync($"Courts/{courtId}/Update", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetSeasonsResult>> GetSeasons(int clubId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("Seasons", cancellationToken)).AsHttpResult<GetSeasonsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> AddSeason(int clubId, SeasonRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("Seasons", request, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateSeason(int clubId, int seasonId, SeasonRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Seasons/{seasonId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteSeason(int clubId, int seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Seasons/{seasonId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}