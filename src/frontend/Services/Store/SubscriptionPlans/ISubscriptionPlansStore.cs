using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Client.Services.Store.SubscriptionPlans;

public interface ISubscriptionPlansStore : ISemaphoreStore
{
    event Action? OnPlansChanged;
    event Action? OnPlanChanged;

    GetSubscriptionPlansResult? Plans { get; }

    /// <summary>The plan currently opened in the planner.</summary>
    SubscriptionPlanDto? Plan { get; }

    Task LoadPlans();
    Task LoadPlan(int subscriptionPlanId);
    Task<HttpResult<int>> CreatePlan(CreateSubscriptionPlanModel model);
    Task<HttpResult> UpdatePlan(int subscriptionPlanId, UpdateSubscriptionPlanModel model);
    Task<HttpResult> DeletePlan(int subscriptionPlanId);
    Task<HttpResult> ComputeSchedule(int subscriptionPlanId, int? seed);
    Task<HttpResult> UpdateSchedule(int subscriptionPlanId, UpdateSubscriptionScheduleModel model);
    Task<HttpResult<FileDownload>> ExportPlan(int subscriptionPlanId);
}
