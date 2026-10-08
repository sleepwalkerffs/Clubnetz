using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Clubs;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Client.Services.Store.ClubProfile;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Club;

namespace Bookennis.Client.Services.Store.Club;

public class ClubStore(IClubsHttpClient clubsHttpClient, IClubProfileStore clubProfileStore) : SemaphoreStore, IClubStore
{
    public event Action? OnDetailsChanged;
    public event Action? OnPlayModesChanged;
    public event Action? OnCourtsDataChanged;
    public event Action? OnPlayersDataChanged;
    public event Action? OnSeasonsChanged;

    public ClubInformationResult ClubDetails { get; private set; } = null!;
    public List<PlayerResult> ClubPlayers { get; private set; } = [];
    public List<PlayerResult> GuestPlayers { get; private set; } = [];
    public List<CourtResult> Courts { get; private set; } = [];

    public List<PlayModeDto> PlayModes { get; private set; } = [];
    public List<SeasonResult> Seasons { get; private set; } = [];

    public Task LoadClubDetails() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var clubResult = await clubsHttpClient.GetClubInformation(clubProfileStore.SelectedClubId, cancellationToken);
        if (clubResult is { Success: true, Dto: not null })
        {
            ClubDetails = clubResult.Dto;
            OnDetailsChanged?.Invoke();
        }
    }, nameof(LoadClubDetails));

    public async Task<HttpResult> SaveClubDetails(ClubInformationModel model) => await RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.UpdateClubInformation(clubProfileStore.SelectedClubId, model, cancellationToken);
        if (result.Success)
        {
            ClubDetails = new ClubInformationResult()
            {
                OpeningHours = model.OpeningHours,
                PrimeTimeSettings = model.PrimeTimeSettings,
                BookingGracePeriodInMinutes = model.BookingGracePeriodInMinutes
            };
            OnDetailsChanged?.Invoke();
        }
        return result;
    }, nameof(SaveClubDetails));

    public Task LoadPlayModes() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var playModeResult = await clubsHttpClient.GetPlayModes(clubProfileStore.SelectedClubId, cancellationToken);
        if (playModeResult is { Success: true, Dto: not null })
        {
            PlayModes = playModeResult.Dto.PlayModes;
            OnPlayModesChanged?.Invoke();
        }
    }, nameof(LoadPlayModes));

    public Task<HttpResult> AddPlayMode(PlayModeRequest request) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.AddPlayMode(clubProfileStore.SelectedClubId, request, cancellationToken);
        if (result.Success)
        {
            await LoadPlayModes();
        }

        return result;
    }, nameof(AddPlayMode));

    public Task<HttpResult> UpdatePlayMode(int playModeId, PlayModeRequest request) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.UpdatePlayMode(clubProfileStore.SelectedClubId, playModeId, request, cancellationToken);
        if (result.Success)
        {
            await LoadPlayModes();
        }

        return result;
    }, nameof(UpdatePlayMode));

    public Task<HttpResult> DeletePlayMode(int playModeId) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.DeletePlayMode(clubProfileStore.SelectedClubId, playModeId, cancellationToken);
        if (result.Success)
        {
            await LoadPlayModes();
        }

        return result;
    }, nameof(DeletePlayMode));

    public Task LoadCourts() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var courts = await clubsHttpClient.GetCourts(clubProfileStore.SelectedClubId, cancellationToken);
        if (courts is { Success: true, Dto: not null })
        {
            Courts = courts.Dto.Courts;
            OnCourtsDataChanged?.Invoke();
        }
    }, nameof(LoadCourts));

    public Task<HttpResult> UpdateCourt(int courtId, string courtName, string alias, DateTimeOffsetInterval? inactive, int sortOrder) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.UpdateCourt(courtId, new UpdateCourtRequest(courtName, alias, inactive, sortOrder), cancellationToken);
        if (result.Success)
        {
            Courts[Courts.IndexOf(Courts.Single(x => x.CourtId == courtId))] = new CourtResult()
            {
                CourtId = courtId,
                CulbId = clubProfileStore.SelectedClubId,
                Name = courtName,
                Inactive = inactive,
                Alias = alias,
                SortOrder = sortOrder,
            };
            Courts = Courts.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
            OnCourtsDataChanged?.Invoke();
        }

        return result;
    }, nameof(UpdateCourt));

    public Task LoadPlayers() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var players = await clubsHttpClient.GetPlayers(clubProfileStore.SelectedClubId, cancellationToken);
        if (players is { Success: true, Dto: not null })
        {
            ClubPlayers = players.Dto.ClubMembers;
            GuestPlayers = players.Dto.GuestMembers;
            OnPlayersDataChanged?.Invoke();
        }
    }, nameof(LoadPlayers));

    public void ClearClubDetails() => ClubDetails = null!;
    public void ClearClubPlayers() => ClubPlayers.Clear();
    public void ClearCourts() => Courts.Clear();

    public Task LoadSeasons() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.GetSeasons(clubProfileStore.SelectedClubId, cancellationToken);
        if (result is { Success: true, Dto: not null })
        {
            Seasons = result.Dto.Seasons;
            OnSeasonsChanged?.Invoke();
        }
    }, nameof(LoadSeasons));

    public Task<HttpResult> AddSeason(SeasonRequest request) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.AddSeason(clubProfileStore.SelectedClubId, request, cancellationToken);
        if (result.Success)
        {
            await LoadSeasons();
        }
        return result;
    }, nameof(AddSeason));

    public Task<HttpResult> UpdateSeason(int seasonId, SeasonRequest request) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.UpdateSeason(clubProfileStore.SelectedClubId, seasonId, request, cancellationToken);
        if (result.Success)
        {
            await LoadSeasons();
        }
        return result;
    }, nameof(UpdateSeason));

    public Task<HttpResult> DeleteSeason(int seasonId) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await clubsHttpClient.DeleteSeason(clubProfileStore.SelectedClubId, seasonId, cancellationToken);
        if (result.Success)
        {
            await LoadSeasons();
        }
        return result;
    }, nameof(DeleteSeason));
}