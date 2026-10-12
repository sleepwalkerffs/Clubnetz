using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Client.Services.Store.Memberships;

/// <summary>The price list of the club: the membership plans of a season, as the treasurer manages them.</summary>
public interface IMembershipPlansStore : ISemaphoreStore
{
    event Action? OnPlansChanged;

    /// <summary>The plans of the season that was loaded last, in the order members see them; <c>null</c> until they were loaded.</summary>
    List<MembershipPlanDto>? Plans { get; }

    /// <summary>The season <see cref="Plans"/> belongs to.</summary>
    int? SeasonId { get; }

    Task LoadPlans(int seasonId);
    Task<HttpResult> CreatePlan(int seasonId, SaveMembershipPlanModel model);
    Task<HttpResult> UpdatePlan(int planId, SaveMembershipPlanModel model);
    Task<HttpResult> DeletePlan(int planId);
    Task<HttpResult> CopyPlans(int sourceSeasonId, int targetSeasonId);
    Task<HttpResult> SortPlans(int seasonId, List<int> planIds);
}
