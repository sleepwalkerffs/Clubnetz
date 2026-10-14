using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Memberships;

public class MembershipPlanTests
{
    [Fact]
    public void Constructor_TrimsTextsAndKeepsTheRules()
    {
        var plan = new MembershipPlan(1, 5, Data() with { Name = "  Youth  ", Description = "  ", BirthYearFrom = 2009, BirthYearTo = 2013 }, 3);

        plan.ClubId.Should().Be(1);
        plan.SeasonId.Should().Be(5);
        plan.Name.Should().Be("Youth");
        plan.Description.Should().BeNull();
        plan.PriceCents.Should().Be(12000);
        plan.Currency.Should().Be("eur");
        plan.SortOrder.Should().Be(3);
        plan.IsGroupPlan.Should().BeFalse();
        plan.IsFree.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    public void IsGroupPlan_CoupleAndFamilyCoverSeveralMembers(int maxAdults, bool includesChildren, bool isGroupPlan)
    {
        var plan = new MembershipPlan(1, 5, Data() with { MaxAdults = maxAdults, IncludesChildren = includesChildren }, 1);

        plan.IsGroupPlan.Should().Be(isGroupPlan);
    }

    [Fact]
    public void Update_InvalidValues_ThrowWithAnErrorCode()
    {
        ShouldFail(Data() with { Name = " " }, MembershipPlan.ErrorCode.MembershipPlanNameRequired);
        ShouldFail(Data() with { Name = new string('x', 101) }, MembershipPlan.ErrorCode.MembershipPlanNameTooLong);
        ShouldFail(Data() with { Description = new string('x', 1001) }, MembershipPlan.ErrorCode.MembershipPlanDescriptionTooLong);
        ShouldFail(Data() with { PriceCents = -1 }, MembershipPlan.ErrorCode.MembershipPlanInvalidPrice);
        ShouldFail(Data() with { PriceCents = MembershipPlan.MaxPriceCents + 1 }, MembershipPlan.ErrorCode.MembershipPlanInvalidPrice);
        ShouldFail(Data() with { MaxAdults = 0 }, MembershipPlan.ErrorCode.MembershipPlanInvalidMaxAdults);
        ShouldFail(Data() with { MaxAdults = 3 }, MembershipPlan.ErrorCode.MembershipPlanInvalidMaxAdults);
        ShouldFail(Data() with { BirthYearFrom = 2013, BirthYearTo = 2009 }, MembershipPlan.ErrorCode.MembershipPlanInvalidBirthYears);
        ShouldFail(Data() with { BirthYearFrom = 13 }, MembershipPlan.ErrorCode.MembershipPlanInvalidBirthYears);
        ShouldFail(Data() with { AvailableFrom = new DateOnly(2026, 5, 1), AvailableUntil = new DateOnly(2026, 4, 1) }, MembershipPlan.ErrorCode.MembershipPlanInvalidAvailability);
        ShouldFail(Data() with { MaxAdults = 2, RequiresEnrolledFamilyAdult = true }, MembershipPlan.ErrorCode.MembershipPlanFamilyDiscountOnlyForOneMember);
    }

    [Fact]
    public void Update_FreePlan_IsAllowed()
    {
        var plan = new MembershipPlan(1, 5, Data() with { PriceCents = 0 }, 1);

        plan.IsFree.Should().BeTrue();
    }

    [Fact]
    public void IsAvailableOn_RespectsBothEnds()
    {
        var plan = new MembershipPlan(1, 5, Data() with { AvailableFrom = new DateOnly(2026, 2, 1), AvailableUntil = new DateOnly(2026, 3, 31) }, 1);

        plan.IsAvailableOn(new DateOnly(2026, 1, 31)).Should().BeFalse();
        plan.IsAvailableOn(new DateOnly(2026, 2, 1)).Should().BeTrue();
        plan.IsAvailableOn(new DateOnly(2026, 3, 31)).Should().BeTrue();
        plan.IsAvailableOn(new DateOnly(2026, 4, 1)).Should().BeFalse();
    }

    [Fact]
    public void CopyData_MovesBirthYearsAndDatesWithTheSeason()
    {
        var plan = new MembershipPlan(1, 5, Data() with
        {
            Name = "Youth",
            BirthYearFrom = 2009,
            BirthYearTo = 2013,
            AvailableUntil = new DateOnly(2026, 3, 31),
            RequiresApproval = true,
        }, 1);

        var copy = plan.CopyData(yearOffset: 1);

        copy.Should().Be(Data() with
        {
            Name = "Youth",
            BirthYearFrom = 2010,
            BirthYearTo = 2014,
            AvailableUntil = new DateOnly(2027, 3, 31),
            RequiresApproval = true,
        });
    }

    private static void ShouldFail(MembershipPlan.PlanData data, MembershipPlan.ErrorCode errorCode)
    {
        var act = () => new MembershipPlan(1, 5, data, 1);
        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(errorCode.ToString());
    }

    private static MembershipPlan.PlanData Data()
        => new("Adult", "Full member", 12000, 1, false, null, null, false, false, false, true, null, null, true);
}
