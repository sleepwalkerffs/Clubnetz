using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Client.Utils.Members;
using Bookennis.Client.Utils.Pagination;
using Bookennis.Client.Utils.Sorting;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;
using Microsoft.AspNetCore.WebUtilities;

namespace Bookennis.Client.Services.HttpClients.Members;

public class MemberHttpService(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IMemberHttpService
{
    public async Task<HttpResult<GetMembersResult>> GetMembers(PaginationParameters paginationParameters, SortParameters? sortParameters, MemberFilter filter, CancellationToken cancellationToken = default)
    {
        var queryParams = paginationParameters.ToQueryParameters();
        queryParams.AddSortParameters(sortParameters);

        var queryPairs = queryParams.AsEnumerable().Concat(filter.ToQueryPairs());
        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString("", queryPairs), cancellationToken)).AsHttpResult<GetMembersResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<GetMemberEmailsResult>> GetMemberEmails(MemberFilter filter, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(QueryHelpers.AddQueryString("emails", filter.ToQueryPairs()), cancellationToken)).AsHttpResult<GetMemberEmailsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetMembersSummaryResult>> GetMembersSummary(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("summary", cancellationToken)).AsHttpResult<GetMembersSummaryResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetMemberOverviewResult>> GetMemberOverview(int memberId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{memberId}/overview", cancellationToken)).AsHttpResult<GetMemberOverviewResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<MembersDetailResult>> GetMember(int memberId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"{memberId}", cancellationToken)).AsHttpResult<MembersDetailResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateMemberBookingOptions(int memberId, int[] allowedSeasonIds, int bookingsPerWeek, CancellationToken cancellationToken)
        => await (await httpClient.PatchAsJsonAsync($"{memberId}/BookingOptions", new UpdateMemberBookingOptionsRequest(allowedSeasonIds, bookingsPerWeek), jsonOptions, cancellationToken))
              .AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateMemberRoles(int memberId, MemberRole[] roles, CancellationToken cancellationToken)
        => await (await httpClient.PatchAsJsonAsync($"{memberId}/Roles", new UpdateMemberRolesRequest { Roles = roles }, jsonOptions, cancellationToken))
              .AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetMemberBookingHistoryResult>> GetBookingHistory(int memberId, int seasonId, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string> { { "seasonId", seasonId.ToString() } };
        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString($"{memberId}/BookingHistory", query!), cancellationToken))
              .AsHttpResult<GetMemberBookingHistoryResult>(jsonOptions, cancellationToken);
    }
}