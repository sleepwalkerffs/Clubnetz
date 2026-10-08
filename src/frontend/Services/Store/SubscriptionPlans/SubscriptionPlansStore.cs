using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.SubscriptionPlans;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.SubscriptionPlans;

namespace Bookennis.Client.Services.Store.SubscriptionPlans;

public class SubscriptionPlansStore(ISubscriptionPlansHttpClient httpClient) : SemaphoreStore, ISubscriptionPlansStore
{
    public event Action? OnPlansChanged;
    public event Action? OnPlanChanged;

    public GetSubscriptionPlansResult? Plans { get; private set; }
    public SubscriptionPlanDto? Plan { get; private set; }

    public Task LoadPlans() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetSubscriptionPlans(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Plans = response.Dto;
            OnPlansChanged?.Invoke();
        }
    }, nameof(LoadPlans));

    public Task LoadPlan(int subscriptionPlanId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Plan?.Id != subscriptionPlanId)
        {
            Plan = null;
            OnPlanChanged?.Invoke();
        }

        var response = await httpClient.GetSubscriptionPlan(subscriptionPlanId, cancellationToken);
        if (response is { Success: true, Dto: not null })
            SetPlan(response.Dto);
    }, nameof(LoadPlan));

    public async Task<HttpResult<int>> CreatePlan(CreateSubscriptionPlanModel model)
        => (HttpResult<int>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.CreateSubscriptionPlan(model, cancellationToken);
            if (response.Success)
                await LoadPlans();
            return response;
        }, nameof(CreatePlan));

    public Task<HttpResult> UpdatePlan(int subscriptionPlanId, UpdateSubscriptionPlanModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.UpdateSubscriptionPlan(subscriptionPlanId, model, cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetPlan(response.Dto);
            return response;
        }, nameof(UpdatePlan));

    public Task<HttpResult> DeletePlan(int subscriptionPlanId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.DeleteSubscriptionPlan(subscriptionPlanId, cancellationToken);
            if (response.Success)
                await LoadPlans();
            return response;
        }, nameof(DeletePlan));

    public Task<HttpResult> ComputeSchedule(int subscriptionPlanId, int? seed)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.ComputeSubscriptionSchedule(subscriptionPlanId, new ComputeSubscriptionScheduleModel(seed), cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetPlan(response.Dto);
            return response;
        }, nameof(ComputeSchedule));

    public Task<HttpResult> UpdateSchedule(int subscriptionPlanId, UpdateSubscriptionScheduleModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.UpdateSubscriptionSchedule(subscriptionPlanId, model, cancellationToken);
            if (response is { Success: true, Dto: not null })
                SetPlan(response.Dto);
            return response;
        }, nameof(UpdateSchedule));

    public async Task<HttpResult<FileDownload>> ExportPlan(int subscriptionPlanId)
        => (HttpResult<FileDownload>)await RunInSavingContextAsync(
            async cancellationToken => await httpClient.ExportSubscriptionPlan(subscriptionPlanId, cancellationToken),
            nameof(ExportPlan));

    private void SetPlan(SubscriptionPlanDto plan)
    {
        Plan = plan;
        OnPlanChanged?.Invoke();
    }
}
