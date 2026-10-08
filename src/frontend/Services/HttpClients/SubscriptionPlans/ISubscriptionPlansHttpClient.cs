using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Client.Services.HttpClients.SubscriptionPlans;

public interface ISubscriptionPlansHttpClient
{
    public Task<HttpResult<GetSubscriptionPlansResult>> GetSubscriptionPlans(CancellationToken cancellationToken = default);
    public Task<HttpResult<SubscriptionPlanDto>> GetSubscriptionPlan(int subscriptionPlanId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateSubscriptionPlan(CreateSubscriptionPlanModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<SubscriptionPlanDto>> UpdateSubscriptionPlan(int subscriptionPlanId, UpdateSubscriptionPlanModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteSubscriptionPlan(int subscriptionPlanId, CancellationToken cancellationToken = default);
    public Task<HttpResult<SubscriptionPlanDto>> ComputeSubscriptionSchedule(int subscriptionPlanId, ComputeSubscriptionScheduleModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<SubscriptionPlanDto>> UpdateSubscriptionSchedule(int subscriptionPlanId, UpdateSubscriptionScheduleModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<FileDownload>> ExportSubscriptionPlan(int subscriptionPlanId, CancellationToken cancellationToken = default);
}
