using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Admin;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Services.Store.Admin;

public class AdminStore(IAdminHttpClient adminHttpClient) : SemaphoreStore, IAdminStore
{
    // Users
    public event Action? OnUsersChanged;
    public event Action? OnUserDetailChanged;
    public bool UsersLoaded { get; private set; }
    public List<AdminUserResult> Users { get; private set; } = [];
    public int TotalUsers { get; private set; }
    public AdminUserDetailResult? UserDetail { get; private set; }

    public Task LoadUsers(PaginationParameters pagination, SortParameters? sort, string? searchTerm)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            UsersLoaded = false;
            var result = await adminHttpClient.GetUsers(pagination, sort, searchTerm, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Users = result.Dto.Entities;
                TotalUsers = result.Dto.Total;
                UsersLoaded = true;
                OnUsersChanged?.Invoke();
            }
        }, nameof(LoadUsers));

    public async Task<List<AdminUserResult>> SearchUsers(string searchTerm)
    {
        var result = await adminHttpClient.SearchUsers(searchTerm);
        return result is { Success: true, Dto: not null } ? result.Dto.Entities : [];
    }

    public Task LoadUser(int userId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.GetUser(userId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                UserDetail = result.Dto;
                OnUserDetailChanged?.Invoke();
            }
        }, nameof(LoadUser));

    public async Task<bool> UpdateUser(int userId, UpdateAdminUserRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdateUser(userId, request, cancellationToken), nameof(UpdateUser));
        return response.Success;
    }

    public async Task<bool> DeleteUser(int userId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.DeleteUser(userId, cancellationToken), nameof(DeleteUser));
        return response.Success;
    }

    public async Task<bool> ResendConfirmationEmail(int userId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.ResendConfirmationEmail(userId, cancellationToken), nameof(ResendConfirmationEmail));
        return response.Success;
    }

    public async Task<bool> ResetPassword(int userId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.ResetPassword(userId, cancellationToken), nameof(ResetPassword));
        return response.Success;
    }

    public async Task<bool> ChangeEmail(int userId, ChangeUserEmailRequest request)
    {

        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.ChangeEmail(userId, request, cancellationToken), nameof(ChangeEmail));
        return response.Success;
    }

    public async Task<bool> ImpersonateUser(int userId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.ImpersonateUser(userId, cancellationToken), nameof(ImpersonateUser));
        return response.Success;
    }

    // Clubs
    public event Action? OnClubsChanged;
    public event Action? OnClubDetailChanged;
    public bool ClubsLoaded { get; private set; }
    public List<AdminClubResult> Clubs { get; private set; } = [];
    public AdminClubDetailResult? ClubDetail { get; private set; }

    public Task LoadClubs()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            ClubsLoaded = false;
            var result = await adminHttpClient.GetClubs(cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Clubs = result.Dto;
                ClubsLoaded = true;
                OnClubsChanged?.Invoke();
            }
        }, nameof(LoadClubs));

    public Task LoadClub(int clubId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.GetClub(clubId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                ClubDetail = result.Dto;
                OnClubDetailChanged?.Invoke();
            }
        }, nameof(LoadClub));

    public async Task<int?> CreateClub(CreateClubRequest request)
    {
        int? clubId = null;
        await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.CreateClub(request, cancellationToken);
            if (result is { Success: true })
                clubId = result.Dto;
            return result;
        }, nameof(CreateClub));
        return clubId;
    }

    public async Task<bool> UpdateClub(int clubId, UpdateAdminClubRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdateClub(clubId, request, cancellationToken), nameof(UpdateClub));
        return response.Success;
    }

    public async Task<bool> DeleteClub(int clubId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.DeleteClub(clubId, cancellationToken), nameof(DeleteClub));
        return response.Success;
    }

    // PlayModes
    public async Task<bool> AddPlayMode(int clubId, AdminPlayModeRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.AddPlayMode(clubId, request, cancellationToken), nameof(AddPlayMode));
        return response.Success;
    }

    public async Task<bool> UpdatePlayMode(int clubId, int playModeId, AdminPlayModeRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdatePlayMode(clubId, playModeId, request, cancellationToken), nameof(UpdatePlayMode));
        return response.Success;
    }

    public async Task<bool> DeletePlayMode(int clubId, int playModeId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.DeletePlayMode(clubId, playModeId, cancellationToken), nameof(DeletePlayMode));
        return response.Success;
    }

    // Club Members
    public event Action? OnClubMembersChanged;
    public List<AdminClubMemberResult> ClubMembers { get; private set; } = [];
    public int TotalClubMembers { get; private set; }

    public Task LoadClubMembers(int clubId, PaginationParameters pagination, string? searchTerm)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.GetClubMembers(clubId, pagination, searchTerm, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                ClubMembers = result.Dto.Entities;
                TotalClubMembers = result.Dto.Total;
                OnClubMembersChanged?.Invoke();
            }
        }, nameof(LoadClubMembers));

    public async Task<bool> AddClubMember(int clubId, AddClubMemberRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.AddClubMember(clubId, request, cancellationToken), nameof(AddClubMember));
        return response.Success;
    }

    public async Task<bool> UpdateClubMember(int clubId, int memberId, UpdateClubMemberRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdateClubMember(clubId, memberId, request, cancellationToken), nameof(UpdateClubMember));
        return response.Success;
    }

    public async Task<bool> RemoveClubMember(int clubId, int memberId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.RemoveClubMember(clubId, memberId, cancellationToken), nameof(RemoveClubMember));
        return response.Success;
    }

    // Families
    public event Action? OnFamiliesChanged;
    public List<AdminFamilyResult> Families { get; private set; } = [];
    public int TotalFamilies { get; private set; }

    public Task LoadClubFamilies(int clubId, PaginationParameters pagination, string? searchTerm)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.GetClubFamilies(clubId, pagination, searchTerm, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Families = result.Dto.Entities;
                TotalFamilies = result.Dto.Total;
                OnFamiliesChanged?.Invoke();
            }
        }, nameof(LoadClubFamilies));

    public async Task<bool> UpdateFamilyMember(int clubId, int memberId, UpdateFamilyMemberRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdateFamilyMember(clubId, memberId, request, cancellationToken), nameof(UpdateFamilyMember));
        return response.Success;
    }

    // Courts
    public event Action? OnCourtsChanged;
    public List<AdminCourtResult> Courts { get; private set; } = [];

    public Task LoadClubCourts(int clubId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.GetClubCourts(clubId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Courts = result.Dto;
                OnCourtsChanged?.Invoke();
            }
        }, nameof(LoadClubCourts));

    public async Task<bool> AddCourt(int clubId, AdminCourtRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.AddCourt(clubId, request, cancellationToken), nameof(AddCourt));
        return response.Success;
    }

    public async Task<bool> UpdateCourt(int clubId, int courtId, AdminCourtRequest request)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdateCourt(clubId, courtId, request, cancellationToken), nameof(UpdateCourt));
        return response.Success;
    }

    public async Task<bool> DeleteCourt(int clubId, int courtId)
    {
        var response = await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.DeleteCourt(clubId, courtId, cancellationToken), nameof(DeleteCourt));
        return response.Success;
    }

    // Seasons
    public event Action? OnSeasonsChanged;
    public List<AdminSeasonResult> Seasons { get; private set; } = [];

    public Task LoadClubSeasons(int clubId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await adminHttpClient.GetClubSeasons(clubId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Seasons = result.Dto;
                OnSeasonsChanged?.Invoke();
            }
        }, nameof(LoadClubSeasons));

    public async Task<HttpResult> AddSeason(int clubId, AdminSeasonRequest request)
    {
        return await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.AddSeason(clubId, request, cancellationToken), nameof(AddSeason));
    }

    public async Task<HttpResult> UpdateSeason(int clubId, int seasonId, AdminSeasonRequest request)
    {
        return await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.UpdateSeason(clubId, seasonId, request, cancellationToken), nameof(UpdateSeason));
    }

    public async Task<HttpResult> DeleteSeason(int clubId, int seasonId)
    {
        return await RunInSavingContextAsync(async cancellationToken =>
            await adminHttpClient.DeleteSeason(clubId, seasonId, cancellationToken), nameof(DeleteSeason));
    }
}
