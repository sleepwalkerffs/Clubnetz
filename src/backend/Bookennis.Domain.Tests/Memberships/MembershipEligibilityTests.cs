using Bookennis.Domain.Memberships;
using FluentAssertions;
using Xunit;
using static Bookennis.Domain.Memberships.MembershipEligibility;

namespace Bookennis.Domain.Tests.Memberships;

/// <summary>
/// The lines of the price lists of three real clubs (season 2026), each with somebody who may choose it and somebody who may not.
/// </summary>
public class MembershipEligibilityTests
{
    private const int FamilyId = 7;
    private static readonly DateOnly Today = new(2026, 3, 15);

    // ----- one member -----

    [Fact]
    public void Adult_AnyMember_IsEligible()
    {
        Check(Plan(), [Member(1, 1985)]).Should().BeEmpty();
        Check(Plan(), [Member(1, 2012, FamilyRole.Child)]).Should().BeEmpty("a plan without age limit is open to everybody");
    }

    [Fact]
    public void Adult_TwoMembers_OnlyOneMember()
    {
        Check(Plan(), [Member(1, 1985, FamilyRole.Parent), Member(2, 1987, FamilyRole.Parent)]).Should().Equal(ErrorCode.OnlyOneMember);
    }

    [Theory]
    [InlineData(2009, true)]
    [InlineData(2013, true)]
    [InlineData(2008, false)]
    [InlineData(2014, false)]
    public void Youth_BornBetween2009And2013(int birthYear, bool eligible)
    {
        var plan = Plan(p => p with { BirthYearFrom = 2009, BirthYearTo = 2013 });

        IsEligible(plan, Today, [Member(1, birthYear, FamilyRole.Child)], []).Should().Be(eligible);
    }

    [Theory]
    [InlineData(2014, true)]
    [InlineData(2020, true)]
    [InlineData(2013, false)]
    public void Child_BornFrom2014(int birthYear, bool eligible)
    {
        var plan = Plan(p => p with { BirthYearFrom = 2014 });

        IsEligible(plan, Today, [Member(1, birthYear, FamilyRole.Child)], []).Should().Be(eligible);
    }

    [Theory]
    [InlineData(1956, true)]
    [InlineData(1940, true)]
    [InlineData(1957, false)]
    public void Senior_70AndOlder(int birthYear, bool eligible)
    {
        var plan = Plan(p => p with { BirthYearTo = 1956 });

        IsEligible(plan, Today, [Member(1, birthYear)], []).Should().Be(eligible);
    }

    [Fact]
    public void AgeLimit_BirthdayUnknown_BirthYearOutOfRange()
    {
        var plan = Plan(p => p with { BirthYearTo = 1956 });

        Check(plan, [Member(1, null)]).Should().Equal(ErrorCode.BirthYearOutOfRange);
    }

    [Fact]
    public void KidsUpTo8_FreePlan_IsEligibleLikeAnyOtherPlan()
    {
        var plan = Plan(p => p with { PriceCents = 0, BirthYearFrom = 2018 });

        plan.IsFree.Should().BeTrue();
        Check(plan, [Member(1, 2019, FamilyRole.Child)]).Should().BeEmpty();
        Check(plan, [Member(1, 2015, FamilyRole.Child)]).Should().Equal(ErrorCode.BirthYearOutOfRange);
    }

    [Fact]
    public void Student_NeedsConfirmation_IsNotARuleOfEligibility()
    {
        var plan = Plan(p => p with { RequiresApproval = true });

        // Whether somebody is a student is decided by the treasurer, the plan itself can be asked for by anybody
        Check(plan, [Member(1, 2003)]).Should().BeEmpty();
    }

    [Fact]
    public void NewMember_NeverEnrolledBefore()
    {
        var plan = Plan(p => p with { NewMembersOnly = true });

        Check(plan, [Member(1, 1990)]).Should().BeEmpty();
        Check(plan, [Member(1, 1990) with { EnrolledInEarlierSeason = true }]).Should().Equal(ErrorCode.NotANewMember);
    }

    [Fact]
    public void Passive_IsChosenLikeAnyPlanAndDoesNotEnroll()
    {
        var plan = Plan(p => p with { GrantsSeasonEnrollment = false });

        plan.GrantsSeasonEnrollment.Should().BeFalse();
        Check(plan, [Member(1, 1960)]).Should().BeEmpty();
    }

    [Fact]
    public void EarlyBird_OnlyUntilItsLastDay()
    {
        var plan = Plan(p => p with { AvailableUntil = new DateOnly(2026, 3, 31) });

        MembershipEligibility.Check(plan, new DateOnly(2026, 3, 31), [Member(1, 1990)], []).Should().BeEmpty();
        MembershipEligibility.Check(plan, new DateOnly(2026, 4, 1), [Member(1, 1990)], []).Should().Equal(ErrorCode.PlanNotAvailable);
    }

    [Fact]
    public void HalfSeason_OnlyFromItsFirstDay()
    {
        var plan = Plan(p => p with { AvailableFrom = new DateOnly(2026, 7, 15) });

        MembershipEligibility.Check(plan, new DateOnly(2026, 7, 14), [Member(1, 1990)], []).Should().Equal(ErrorCode.PlanNotAvailable);
        MembershipEligibility.Check(plan, new DateOnly(2026, 7, 15), [Member(1, 1990)], []).Should().BeEmpty();
    }

    // ----- plans that depend on the family -----

    [Fact]
    public void PartnerCard_PartnerHasAMembership_IsEligible()
    {
        var plan = Plan(p => p with { RequiresEnrolledFamilyAdult = true });

        Check(plan, [Member(2, 1987, FamilyRole.Parent)], [new FamilyAdult(1, HasMembership: true)]).Should().BeEmpty();
    }

    [Fact]
    public void PartnerCard_PartnerHasNoMembershipOrThereIsNoPartner_NoEnrolledFamilyAdult()
    {
        var plan = Plan(p => p with { RequiresEnrolledFamilyAdult = true });

        Check(plan, [Member(2, 1987, FamilyRole.Parent)], [new FamilyAdult(1, HasMembership: false)]).Should().Equal(ErrorCode.NoEnrolledFamilyAdult);
        Check(plan, [Member(2, 1987)]).Should().Equal(ErrorCode.NoEnrolledFamilyAdult);
    }

    [Fact]
    public void ChildOfAMember_HalfPrice_NeedsAParentWithMembershipAndTheRightAge()
    {
        var plan = Plan(p => p with { RequiresEnrolledFamilyAdult = true, BirthYearFrom = 2008 });
        var parentWithMembership = new FamilyAdult(1, HasMembership: true);

        Check(plan, [Member(3, 2012, FamilyRole.Child)], [parentWithMembership]).Should().BeEmpty();
        Check(plan, [Member(3, 2012, FamilyRole.Child)], [new FamilyAdult(1, false), new FamilyAdult(2, false)]).Should().Equal(ErrorCode.NoEnrolledFamilyAdult);
        Check(plan, [Member(3, 2001, FamilyRole.Child)], [parentWithMembership]).Should().Equal(ErrorCode.BirthYearOutOfRange);
    }

    // ----- couples and families -----

    [Fact]
    public void Couple_TwoParentsOfOneFamily_IsEligible()
    {
        Check(Couple(), [Member(1, 1985, FamilyRole.Parent), Member(2, 1987, FamilyRole.Parent)]).Should().BeEmpty();
    }

    [Fact]
    public void Couple_WithAChild_ChildrenNotIncluded()
    {
        Check(Couple(), [Member(1, 1985, FamilyRole.Parent), Member(2, 1987, FamilyRole.Parent), Member(3, 2012, FamilyRole.Child)])
            .Should().Equal(ErrorCode.ChildrenNotIncluded);
    }

    [Fact]
    public void Couple_TwoMembersWhoAreNoFamily_MemberNotInFamily()
    {
        Check(Couple(), [Member(1, 1985), Member(2, 1987)]).Should().Equal(ErrorCode.MemberNotInFamily);
        Check(Couple(), [Member(1, 1985, FamilyRole.Parent), Member(2, 1987, FamilyRole.Parent, familyId: 99)]).Should().Equal(ErrorCode.MemberNotInFamily);
    }

    [Fact]
    public void FamilyWithOneAdult_OneParentAndChildren_IsEligible()
    {
        Check(Family(adults: 1), [Member(1, 1985, FamilyRole.Parent), Member(3, 2012, FamilyRole.Child), Member(4, 2015, FamilyRole.Child)]).Should().BeEmpty();
    }

    [Fact]
    public void FamilyWithOneAdult_BothParents_TooManyAdults()
    {
        Check(Family(adults: 1), [Member(1, 1985, FamilyRole.Parent), Member(2, 1987, FamilyRole.Parent), Member(3, 2012, FamilyRole.Child)])
            .Should().Equal(ErrorCode.TooManyAdults);
    }

    [Fact]
    public void FamilyWithTwoAdults_BothParentsAndChildren_IsEligible()
    {
        Check(Family(adults: 2), [Member(1, 1985, FamilyRole.Parent), Member(2, 1987, FamilyRole.Parent), Member(3, 2012, FamilyRole.Child)]).Should().BeEmpty();
    }

    [Fact]
    public void Family_OnlyChildren_AdultRequired()
    {
        Check(Family(adults: 2), [Member(3, 2012, FamilyRole.Child), Member(4, 2015, FamilyRole.Child)]).Should().Equal(ErrorCode.AdultRequired);
    }

    [Fact]
    public void Family_SingleParentWithoutAFamilyYet_IsEligible()
    {
        Check(Family(adults: 1), [Member(1, 1985)]).Should().BeEmpty("the children can be added to the family later");
    }

    [Fact]
    public void Family_AgeLimitAndNewMemberRuleApplyToTheAdultsOnly()
    {
        var plan = Family(adults: 2, p => p with { BirthYearTo = 2007, NewMembersOnly = true });
        var child = Member(3, 2015, FamilyRole.Child) with { EnrolledInEarlierSeason = true };

        Check(plan, [Member(1, 1985, FamilyRole.Parent), child]).Should().BeEmpty();
        Check(plan, [Member(1, 1985, FamilyRole.Parent) with { EnrolledInEarlierSeason = true }, child]).Should().Equal(ErrorCode.NotANewMember);
    }

    // ----- one membership per season -----

    [Fact]
    public void AnyPlan_AMemberAlreadyHasAMembership_AlreadyHasMembership()
    {
        Check(Plan(), [Member(1, 1985) with { HasMembership = true }]).Should().Equal(ErrorCode.AlreadyHasMembership);
        Check(Family(adults: 2), [Member(1, 1985, FamilyRole.Parent), Member(3, 2012, FamilyRole.Child) with { HasMembership = true }])
            .Should().Equal(ErrorCode.AlreadyHasMembership);
    }

    [Fact]
    public void Check_SeveralRulesBroken_ReturnsAllReasons()
    {
        var plan = Plan(p => p with { BirthYearTo = 1956, NewMembersOnly = true, AvailableUntil = new DateOnly(2026, 1, 31) });

        Check(plan, [Member(1, 1990) with { EnrolledInEarlierSeason = true }])
            .Should().BeEquivalentTo([ErrorCode.PlanNotAvailable, ErrorCode.BirthYearOutOfRange, ErrorCode.NotANewMember]);
    }

    private static IReadOnlyList<ErrorCode> Check(MembershipPlan plan, Candidate[] members, FamilyAdult[]? otherFamilyAdults = null)
        => MembershipEligibility.Check(plan, Today, members, otherFamilyAdults ?? []);

    private static Candidate Member(int id, int? birthYear, FamilyRole role = FamilyRole.None, int? familyId = FamilyId)
        => new(id, birthYear, role, role == FamilyRole.None ? null : familyId, EnrolledInEarlierSeason: false, HasMembership: false);

    private static MembershipPlan Couple() => Plan(p => p with { MaxAdults = 2 });

    private static MembershipPlan Family(int adults, Func<MembershipPlan.PlanData, MembershipPlan.PlanData>? change = null)
        => Plan(p => (change?.Invoke(p) ?? p) with { MaxAdults = adults, IncludesChildren = true });

    private static MembershipPlan Plan(Func<MembershipPlan.PlanData, MembershipPlan.PlanData>? change = null)
    {
        var data = new MembershipPlan.PlanData("Plan", null, 12000, 1, false, null, null, false, false, false, true, null, null, true);
        return new MembershipPlan(1, 1, change?.Invoke(data) ?? data, 1);
    }
}
