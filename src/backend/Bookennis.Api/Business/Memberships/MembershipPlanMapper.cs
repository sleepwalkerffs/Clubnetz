using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Api.Business.Memberships;

public static class MembershipPlanMapper
{
    public static MembershipPlan.PlanData ToPlanData(this SaveMembershipPlanModel model) => new(
        model.Name,
        model.Description,
        model.PriceCents,
        model.MaxAdults,
        model.IncludesChildren,
        model.BirthYearFrom,
        model.BirthYearTo,
        model.RequiresApproval,
        model.NewMembersOnly,
        model.RequiresEnrolledFamilyAdult,
        model.GrantsSeasonEnrollment,
        model.AvailableFrom,
        model.AvailableUntil,
        model.IsSelfService);

    public static MembershipPlanDto ToDto(this MembershipPlan plan, int orderCount = 0) => new()
    {
        Id = plan.Id,
        SeasonId = plan.SeasonId,
        Name = plan.Name,
        Description = plan.Description,
        PriceCents = plan.PriceCents,
        Currency = plan.Currency,
        MaxAdults = plan.MaxAdults,
        IncludesChildren = plan.IncludesChildren,
        BirthYearFrom = plan.BirthYearFrom,
        BirthYearTo = plan.BirthYearTo,
        RequiresApproval = plan.RequiresApproval,
        NewMembersOnly = plan.NewMembersOnly,
        RequiresEnrolledFamilyAdult = plan.RequiresEnrolledFamilyAdult,
        GrantsSeasonEnrollment = plan.GrantsSeasonEnrollment,
        AvailableFrom = plan.AvailableFrom,
        AvailableUntil = plan.AvailableUntil,
        IsSelfService = plan.IsSelfService,
        SortOrder = plan.SortOrder,
        OrderCount = orderCount,
    };
}
