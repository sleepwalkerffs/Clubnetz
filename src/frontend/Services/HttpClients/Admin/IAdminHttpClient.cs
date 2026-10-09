using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Services.HttpClients.Admin;

public interface IAdminHttpClient
{
    // Overview
    public Task<HttpResult<AdminOverviewResult>> GetOverview(CancellationToken cancellationToken = default);

    // Users
    public Task<HttpResult<GetAdminUsersResult>> GetUsers(PaginationParameters pagination, SortParameters? sort, string? searchTerm, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetAdminUsersResult>> SearchUsers(string searchTerm, CancellationToken cancellationToken = default);
    public Task<HttpResult<AdminUserDetailResult>> GetUser(int userId, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateUser(int userId, UpdateAdminUserRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteUser(int userId, CancellationToken cancellationToken = default);
    public Task<HttpResult> ResendConfirmationEmail(int userId, CancellationToken cancellationToken = default);
    public Task<HttpResult> ResetPassword(int userId, CancellationToken cancellationToken = default);
    public Task<HttpResult> ChangeEmail(int userId, ChangeUserEmailRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> ImpersonateUser(int userId, CancellationToken cancellationToken = default);

    // Clubs
    public Task<HttpResult<List<AdminClubResult>>> GetClubs(CancellationToken cancellationToken = default);
    public Task<HttpResult<AdminClubDetailResult>> GetClub(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateClub(CreateClubRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateClub(int clubId, UpdateAdminClubRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteClub(int clubId, CancellationToken cancellationToken = default);

    // PlayModes
    public Task<HttpResult> AddPlayMode(int clubId, AdminPlayModeRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdatePlayMode(int clubId, int playModeId, AdminPlayModeRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeletePlayMode(int clubId, int playModeId, CancellationToken cancellationToken = default);

    // Club Members
    public Task<HttpResult<GetAdminClubMembersResult>> GetClubMembers(int clubId, PaginationParameters pagination, string? searchTerm, CancellationToken cancellationToken = default);
    public Task<HttpResult> AddClubMember(int clubId, AddClubMemberRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateClubMember(int clubId, int memberId, UpdateClubMemberRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> RemoveClubMember(int clubId, int memberId, CancellationToken cancellationToken = default);

    // Families
    public Task<HttpResult<GetAdminClubFamiliesResult>> GetClubFamilies(int clubId, PaginationParameters pagination, string? searchTerm, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateFamilyMember(int clubId, int memberId, UpdateFamilyMemberRequest request, CancellationToken cancellationToken = default);

    // Courts
    public Task<HttpResult<List<AdminCourtResult>>> GetClubCourts(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult> AddCourt(int clubId, AdminCourtRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateCourt(int clubId, int courtId, AdminCourtRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteCourt(int clubId, int courtId, CancellationToken cancellationToken = default);

    // Seasons
    public Task<HttpResult<List<AdminSeasonResult>>> GetClubSeasons(int clubId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> AddSeason(int clubId, AdminSeasonRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateSeason(int clubId, int seasonId, AdminSeasonRequest request, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteSeason(int clubId, int seasonId, CancellationToken cancellationToken = default);
}
