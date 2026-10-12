namespace Bookennis.Domain.Memberships;

/// <summary>
/// Decides whether a plan can be chosen for a set of members. The same rules answer "which plans can this member choose"
/// for the member's page and are checked again when the order is created.
/// </summary>
public static class MembershipEligibility
{
    public enum ErrorCode
    {
        /// <summary>The plan is for other birth years.</summary>
        BirthYearOutOfRange = 0,

        /// <summary>More adults than the plan covers.</summary>
        TooManyAdults = 1,

        /// <summary>The plan does not cover children.</summary>
        ChildrenNotIncluded = 2,

        /// <summary>The plan is for members who were never enrolled before.</summary>
        NotANewMember = 3,

        /// <summary>The plan needs another adult of the family with a membership in the season.</summary>
        NoEnrolledFamilyAdult = 4,

        /// <summary>The plan is not offered today.</summary>
        PlanNotAvailable = 5,

        /// <summary>A plan for several members is for the members of one family.</summary>
        MemberNotInFamily = 6,

        /// <summary>One of the members already has a membership in the season.</summary>
        AlreadyHasMembership = 7,

        /// <summary>The plan is for one member.</summary>
        OnlyOneMember = 8,

        /// <summary>A plan for a couple or a family needs at least one adult.</summary>
        AdultRequired = 9,
    }

    public enum FamilyRole
    {
        None = 0,
        Parent = 1,
        Child = 2,
    }

    /// <summary>
    /// A member the order should cover. <c>BirthYear</c> is <c>null</c> when the birthday is not known, <c>FamilyId</c> without a family.
    /// <c>EnrolledInEarlierSeason</c>: the member was enrolled in a season that started before the season of the plan.
    /// <c>HasMembership</c>: the member is already covered by another order of the season that is not cancelled, failed or refunded.
    /// </summary>
    public record Candidate(int MemberId, int? BirthYear, FamilyRole Role, int? FamilyId, bool EnrolledInEarlierSeason, bool HasMembership);

    /// <summary>
    /// A parent of the candidates' family who is not covered by the order.
    /// <c>HasMembership</c>: enrolled in the season, or covered by an order of the season that is paid or being paid.
    /// </summary>
    public record FamilyAdult(int MemberId, bool HasMembership);

    /// <summary>The reasons why the plan can't be chosen for these members; empty when it can.</summary>
    public static IReadOnlyList<ErrorCode> Check(MembershipPlan plan, DateOnly today, IReadOnlyCollection<Candidate> members, IReadOnlyCollection<FamilyAdult> otherFamilyAdults)
    {
        var reasons = new List<ErrorCode>();

        if (!plan.IsAvailableOn(today))
            reasons.Add(ErrorCode.PlanNotAvailable);

        if (members.Any(m => m.HasMembership))
            reasons.Add(ErrorCode.AlreadyHasMembership);

        // The members the price depends on: the member of a plan for one, the adults of a couple or family
        IReadOnlyCollection<Candidate> ratedMembers;

        if (plan.IsGroupPlan)
        {
            var familyIds = members.Select(m => m.FamilyId).Distinct().ToList();
            if (members.Count > 1 && (familyIds.Count != 1 || familyIds[0] is null))
                reasons.Add(ErrorCode.MemberNotInFamily);

            // Somebody without a family counts as an adult, so a single parent can take a family plan before the children are added
            var adults = members.Where(m => m.Role != FamilyRole.Child).ToList();
            if (adults.Count == 0)
                reasons.Add(ErrorCode.AdultRequired);

            if (adults.Count > plan.MaxAdults)
                reasons.Add(ErrorCode.TooManyAdults);

            if (!plan.IncludesChildren && members.Any(m => m.Role == FamilyRole.Child))
                reasons.Add(ErrorCode.ChildrenNotIncluded);

            ratedMembers = adults;
        }
        else
        {
            if (members.Count > 1)
                reasons.Add(ErrorCode.OnlyOneMember);

            ratedMembers = members;
        }

        if ((plan.BirthYearFrom is not null || plan.BirthYearTo is not null)
         && ratedMembers.Any(m => m.BirthYear is null || m.BirthYear < plan.BirthYearFrom || m.BirthYear > plan.BirthYearTo))
        {
            reasons.Add(ErrorCode.BirthYearOutOfRange);
        }

        if (plan.NewMembersOnly && ratedMembers.Any(m => m.EnrolledInEarlierSeason))
            reasons.Add(ErrorCode.NotANewMember);

        if (plan.RequiresEnrolledFamilyAdult && !otherFamilyAdults.Any(a => a.HasMembership))
            reasons.Add(ErrorCode.NoEnrolledFamilyAdult);

        return reasons;
    }

    public static bool IsEligible(MembershipPlan plan, DateOnly today, IReadOnlyCollection<Candidate> members, IReadOnlyCollection<FamilyAdult> otherFamilyAdults)
        => Check(plan, today, members, otherFamilyAdults).Count == 0;
}
