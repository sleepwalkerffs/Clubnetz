using Bookennis.Shared.Controller.Club;

namespace Bookennis.Client.Services.HttpClients.Clubs;

public interface IClubsHttpClient
{
    public Task<HttpResult<ClubInformationResult>> GetClubInformation(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateClubInformation(int clubId, ClubInformationModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetPlayModesResult>> GetPlayModes(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult> AddPlayMode(int clubId, PlayModeRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdatePlayMode(int clubId, int playModeId, PlayModeRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeletePlayMode(int clubId, int playModeId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetPlayersResult>> GetPlayers(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetCourtsResult>> GetCourts(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateCourt(int courtId, UpdateCourtRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetSeasonsResult>> GetSeasons(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> AddSeason(int clubId, SeasonRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateSeason(int clubId, int seasonId, SeasonRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteSeason(int clubId, int seasonId, CancellationToken cancellationToken = default);
}
