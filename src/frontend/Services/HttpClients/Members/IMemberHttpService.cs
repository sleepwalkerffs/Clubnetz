using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Services.HttpClients.Members;

public interface IMemberHttpService
{
    public Task<HttpResult<GetMembersResult>> GetMembers(PaginationParameters paginationParameters, SortParameters? sortParameters, MemberFilter filter, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetMemberEmailsResult>> GetMemberEmails(MemberFilter filter, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetMembersSummaryResult>> GetMembersSummary(CancellationToken cancellationToken = default);
    public Task<HttpResult<GetMemberOverviewResult>> GetMemberOverview(int memberId, CancellationToken cancellationToken = default);

    public Task<HttpResult<MembersDetailResult>> GetMember(int memberId, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateMemberBookingOptions(int memberId, int[] allowedSeasonIds, int bookingsPerWeek, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateMemberRoles(int memberId, MemberRole[] roles, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetMemberBookingHistoryResult>> GetBookingHistory(int memberId, int seasonId, CancellationToken cancellationToken = default);
}