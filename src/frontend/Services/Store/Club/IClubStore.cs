using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Club;

namespace Bookennis.Client.Services.Store.Club;

public interface IClubStore : ISemaphoreStore
{
    public List<PlayerResult> ClubPlayers { get; }
    public List<PlayerResult> GuestPlayers { get; }
    public List<CourtResult> Courts { get; }
    public List<PlayModeDto> PlayModes { get; }
    public List<SeasonResult> Seasons { get; }
    public event Action OnCourtsDataChanged;
    public event Action OnPlayersDataChanged;
    public event Action OnPlayModesChanged;
    public event Action OnSeasonsChanged;
    public ClubInformationResult ClubDetails { get; }

    public Task LoadClubDetails();
    public Task<HttpResult> SaveClubDetails(ClubInformationModel model);
    public Task LoadPlayModes();
    public Task<HttpResult> AddPlayMode(PlayModeRequest request);
    public Task<HttpResult> UpdatePlayMode(int playModeId, PlayModeRequest request);
    public Task<HttpResult> DeletePlayMode(int playModeId);
    public Task LoadCourts();
    public Task<HttpResult> UpdateCourt(int courtId, string courtName, string alias, DateTimeOffsetInterval? inactive, int sortOrder);
    public void ClearCourts();
    public Task LoadPlayers();
    public void ClearClubPlayers();
    public Task LoadSeasons();
    public Task<HttpResult> AddSeason(SeasonRequest request);
    public Task<HttpResult> UpdateSeason(int seasonId, SeasonRequest request);
    public Task<HttpResult> DeleteSeason(int seasonId);
}