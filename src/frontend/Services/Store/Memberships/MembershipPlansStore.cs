using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Memberships;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.Store.Memberships;

public class MembershipPlansStore(IMembershipPlansHttpClient httpClient) : SemaphoreStore, IMembershipPlansStore
{
    public event Action? OnPlansChanged;

    public List<MembershipPlanDto>? Plans { get; private set; }
    public int? SeasonId { get; private set; }

    public Task LoadPlans(int seasonId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        // Plans of another season must not be shown while the new ones load
        if (SeasonId != seasonId)
        {
            Plans = null;
            SeasonId = seasonId;
            OnPlansChanged?.Invoke();
        }

        var response = await httpClient.GetMembershipPlans(seasonId, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Plans = response.Dto.Plans;
            OnPlansChanged?.Invoke();
        }
    }, nameof(LoadPlans));

    public Task<HttpResult> CreatePlan(int seasonId, SaveMembershipPlanModel model)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.CreateMembershipPlan(seasonId, model, cancellationToken), nameof(CreatePlan));

    public Task<HttpResult> UpdatePlan(int planId, SaveMembershipPlanModel model)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.UpdateMembershipPlan(planId, model, cancellationToken), nameof(UpdatePlan));

    public Task<HttpResult> DeletePlan(int planId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.DeleteMembershipPlan(planId, cancellationToken), nameof(DeletePlan));

    public Task<HttpResult> CopyPlans(int sourceSeasonId, int targetSeasonId)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.CopyMembershipPlans(new CopyMembershipPlansModel(sourceSeasonId, targetSeasonId), cancellationToken), nameof(CopyPlans));

    public Task<HttpResult> SortPlans(int seasonId, List<int> planIds)
        => RunInSavingContextAsync(async cancellationToken => await httpClient.SortMembershipPlans(new SortMembershipPlansModel(seasonId, planIds), cancellationToken), nameof(SortPlans));
}
