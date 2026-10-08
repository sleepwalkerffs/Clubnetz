using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Client.Utils.Pagination;
using Bookennis.Client.Utils.Sorting;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;
using Microsoft.AspNetCore.WebUtilities;

namespace Bookennis.Client.Services.HttpClients.Admin;

public class AdminHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IAdminHttpClient
{
    // Users
    public async Task<HttpResult<GetAdminUsersResult>> GetUsers(PaginationParameters pagination, SortParameters? sort, string? searchTerm, CancellationToken cancellationToken = default)
    {
        var queryParams = pagination.ToQueryParameters();
        queryParams.AddSortParameters(sort);
        if (searchTerm is not null)
            queryParams.Add("searchTerm", searchTerm);

        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString("Users", queryParams!), cancellationToken)).AsHttpResult<GetAdminUsersResult>(jsonOptions, cancellationToken);
    }

    public Task<HttpResult<GetAdminUsersResult>> SearchUsers(string searchTerm, CancellationToken cancellationToken = default)
        => GetUsers(new PaginationParameters { Page = 1, PageSize = 10, EnablePaging = true }, null, searchTerm, cancellationToken);

    public async Task<HttpResult<AdminUserDetailResult>> GetUser(int userId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"Users/{userId}", cancellationToken)).AsHttpResult<AdminUserDetailResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateUser(int userId, UpdateAdminUserRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Users/{userId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteUser(int userId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Users/{userId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ResendConfirmationEmail(int userId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"Users/{userId}/ResendConfirmationEmail", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ResetPassword(int userId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"Users/{userId}/ResetPassword", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ChangeEmail(int userId, ChangeUserEmailRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"Users/{userId}/ChangeEmail", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ImpersonateUser(int userId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"Users/{userId}/Impersonate", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // Clubs
    public async Task<HttpResult<List<AdminClubResult>>> GetClubs(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("Clubs", cancellationToken)).AsHttpResult<List<AdminClubResult>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AdminClubDetailResult>> GetClub(int clubId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"Clubs/{clubId}", cancellationToken)).AsHttpResult<AdminClubDetailResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateClub(CreateClubRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("Clubs", request, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateClub(int clubId, UpdateAdminClubRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Clubs/{clubId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteClub(int clubId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Clubs/{clubId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // PlayModes
    public async Task<HttpResult> AddPlayMode(int clubId, AdminPlayModeRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"Clubs/{clubId}/PlayModes", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdatePlayMode(int clubId, int playModeId, AdminPlayModeRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Clubs/{clubId}/PlayModes/{playModeId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeletePlayMode(int clubId, int playModeId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Clubs/{clubId}/PlayModes/{playModeId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // Club Members
    public async Task<HttpResult<GetAdminClubMembersResult>> GetClubMembers(int clubId, PaginationParameters pagination, string? searchTerm, CancellationToken cancellationToken = default)
    {
        var queryParams = pagination.ToQueryParameters();
        if (searchTerm is not null)
            queryParams.Add("searchTerm", searchTerm);

        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString($"Clubs/{clubId}/Members", queryParams!), cancellationToken)).AsHttpResult<GetAdminClubMembersResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> AddClubMember(int clubId, AddClubMemberRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"Clubs/{clubId}/Members", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateClubMember(int clubId, int memberId, UpdateClubMemberRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Clubs/{clubId}/Members/{memberId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> RemoveClubMember(int clubId, int memberId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Clubs/{clubId}/Members/{memberId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // Families
    public async Task<HttpResult<GetAdminClubFamiliesResult>> GetClubFamilies(int clubId, PaginationParameters pagination, string? searchTerm, CancellationToken cancellationToken = default)
    {
        var queryParams = pagination.ToQueryParameters();
        if (searchTerm is not null)
            queryParams.Add("searchTerm", searchTerm);

        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString($"Clubs/{clubId}/Families", queryParams!), cancellationToken)).AsHttpResult<GetAdminClubFamiliesResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> UpdateFamilyMember(int clubId, int memberId, UpdateFamilyMemberRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Clubs/{clubId}/Families/Members/{memberId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // Courts
    public async Task<HttpResult<List<AdminCourtResult>>> GetClubCourts(int clubId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"Clubs/{clubId}/Courts", cancellationToken)).AsHttpResult<List<AdminCourtResult>>(jsonOptions, cancellationToken);

    public async Task<HttpResult> AddCourt(int clubId, AdminCourtRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"Clubs/{clubId}/Courts", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateCourt(int clubId, int courtId, AdminCourtRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Clubs/{clubId}/Courts/{courtId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteCourt(int clubId, int courtId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Clubs/{clubId}/Courts/{courtId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    // Seasons
    public async Task<HttpResult<List<AdminSeasonResult>>> GetClubSeasons(int clubId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"Clubs/{clubId}/Seasons", cancellationToken)).AsHttpResult<List<AdminSeasonResult>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> AddSeason(int clubId, AdminSeasonRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"Clubs/{clubId}/Seasons", request, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateSeason(int clubId, int seasonId, AdminSeasonRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"Clubs/{clubId}/Seasons/{seasonId}", request, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteSeason(int clubId, int seasonId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"Clubs/{clubId}/Seasons/{seasonId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
