using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Services.Store.Admin;

public interface IAdminStore : ISemaphoreStore
{
    // Overview
    public event Action? OnOverviewChanged;
    public AdminOverviewResult? Overview { get; }
    public Task LoadOverview();

    // Users
    public event Action? OnUsersChanged;
    public event Action? OnUserDetailChanged;
    public bool UsersLoaded { get; }
    public List<AdminUserResult> Users { get; }
    public int TotalUsers { get; }
    public AdminUserDetailResult? UserDetail { get; }

    public Task LoadUsers(PaginationParameters pagination, SortParameters? sort, string? searchTerm);
    public Task<List<AdminUserResult>> SearchUsers(string searchTerm);
    public Task LoadUser(int userId);
    public Task<bool> UpdateUser(int userId, UpdateAdminUserRequest request);
    public Task<bool> DeleteUser(int userId);
    public Task<bool> ResendConfirmationEmail(int userId);
    public Task<bool> ResetPassword(int userId);
    public Task<bool> ChangeEmail(int userId, ChangeUserEmailRequest request);
    public Task<bool> ImpersonateUser(int userId);

    // Clubs
    public event Action? OnClubsChanged;
    public event Action? OnClubDetailChanged;
    public bool ClubsLoaded { get; }
    public List<AdminClubResult> Clubs { get; }
    public AdminClubDetailResult? ClubDetail { get; }

    public Task LoadClubs();
    public Task LoadClub(int clubId);
    public Task<int?> CreateClub(CreateClubRequest request);
    public Task<bool> UpdateClub(int clubId, UpdateAdminClubRequest request);
    public Task<bool> DeleteClub(int clubId);

    // PlayModes
    public Task<bool> AddPlayMode(int clubId, AdminPlayModeRequest request);
    public Task<bool> UpdatePlayMode(int clubId, int playModeId, AdminPlayModeRequest request);
    public Task<bool> DeletePlayMode(int clubId, int playModeId);

    // Club Members
    public event Action? OnClubMembersChanged;
    public List<AdminClubMemberResult> ClubMembers { get; }
    public int TotalClubMembers { get; }
    public Task LoadClubMembers(int clubId, PaginationParameters pagination, string? searchTerm);
    public Task<bool> AddClubMember(int clubId, AddClubMemberRequest request);
    public Task<bool> UpdateClubMember(int clubId, int memberId, UpdateClubMemberRequest request);
    public Task<bool> RemoveClubMember(int clubId, int memberId);

    // Families
    public event Action? OnFamiliesChanged;
    public List<AdminFamilyResult> Families { get; }
    public int TotalFamilies { get; }
    public Task LoadClubFamilies(int clubId, PaginationParameters pagination, string? searchTerm);
    public Task<bool> UpdateFamilyMember(int clubId, int memberId, UpdateFamilyMemberRequest request);

    // Courts
    public event Action? OnCourtsChanged;
    public List<AdminCourtResult> Courts { get; }
    public Task LoadClubCourts(int clubId);
    public Task<bool> AddCourt(int clubId, AdminCourtRequest request);
    public Task<bool> UpdateCourt(int clubId, int courtId, AdminCourtRequest request);
    public Task<bool> DeleteCourt(int clubId, int courtId);

    // Seasons
    public event Action? OnSeasonsChanged;
    public List<AdminSeasonResult> Seasons { get; }
    public Task LoadClubSeasons(int clubId);
    public Task<HttpResult> AddSeason(int clubId, AdminSeasonRequest request);
    public Task<HttpResult> UpdateSeason(int clubId, int seasonId, AdminSeasonRequest request);
    public Task<HttpResult> DeleteSeason(int clubId, int seasonId);
}
