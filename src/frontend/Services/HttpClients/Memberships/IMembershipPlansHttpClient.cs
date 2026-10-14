using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.HttpClients.Memberships;

public interface IMembershipPlansHttpClient
{
    public Task<HttpResult<GetMembershipPlansResult>> GetMembershipPlans(int seasonId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateMembershipPlan(int seasonId, SaveMembershipPlanModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateMembershipPlan(int planId, SaveMembershipPlanModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteMembershipPlan(int planId, CancellationToken cancellationToken = default);
    public Task<HttpResult> CopyMembershipPlans(CopyMembershipPlansModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> SortMembershipPlans(SortMembershipPlansModel model, CancellationToken cancellationToken = default);
}
