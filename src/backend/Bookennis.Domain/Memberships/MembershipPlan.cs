using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Memberships;

/// <summary>
/// A line of the club's price list for one season, e.g. "Adult", "Youth 2009-2013" or "Family".
/// The rules say who may choose it (<see cref="MembershipEligibility"/>); the price is what one order costs, however many members it covers.
/// </summary>
[Guid("0C3B7E0D-5C59-4B3F-9B2B-6D1E7C0A9F11")]
public class MembershipPlan : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 1000;

    /// <summary>Highest price of a plan in cents (99,999.99).</summary>
    public const int MaxPriceCents = 9_999_999;

    public const int MaxAdultsLimit = 2;
    public const int MinBirthYear = 1900;
    public const int MaxBirthYear = 2200;
    public const string DefaultCurrency = "eur";

    public enum ErrorCode
    {
        MembershipPlanNameRequired = 0,
        MembershipPlanNameTooLong = 1,
        MembershipPlanDescriptionTooLong = 2,
        MembershipPlanInvalidPrice = 3,
        MembershipPlanInvalidMaxAdults = 4,
        MembershipPlanInvalidBirthYears = 5,
        MembershipPlanInvalidAvailability = 6,

        /// <summary>The discount for families of a member is for one member (the partner or a child), not for a group.</summary>
        MembershipPlanFamilyDiscountOnlyForOneMember = 7,
    }

    /// <summary>
    /// <c>MaxAdults</c> and <c>IncludesChildren</c> say who one order covers: 1 / false = one member, 2 / false = a couple,
    /// 1 or 2 / true = a family with that many adults and all their children.
    /// </summary>
    public record PlanData(
        string? Name,
        string? Description,
        int PriceCents,
        int MaxAdults,
        bool IncludesChildren,
        int? BirthYearFrom,
        int? BirthYearTo,
        bool RequiresApproval,
        bool NewMembersOnly,
        bool RequiresEnrolledFamilyAdult,
        bool GrantsSeasonEnrollment,
        DateOnly? AvailableFrom,
        DateOnly? AvailableUntil,
        bool IsSelfService);

#pragma warning disable CS8618
    private MembershipPlan() { }
#pragma warning restore CS8618

    public MembershipPlan(int clubId, int seasonId, PlanData data, int sortOrder)
    {
        ClubId = clubId;
        SeasonId = seasonId;
        SortOrder = sortOrder;
        Update(data);
    }

    public int SeasonId { get; private set; }
    public string Name { get; private set; } = "";

    /// <summary>Plain text shown below the name.</summary>
    public string? Description { get; private set; }

    /// <summary>0 is a free plan: it is confirmed instead of paid.</summary>
    public int PriceCents { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    /// <summary>1 or 2. Together with <see cref="IncludesChildren"/> it says who one order covers.</summary>
    public int MaxAdults { get; private set; } = 1;

    public bool IncludesChildren { get; private set; }

    /// <summary>Oldest birth year the plan is for (inclusive); for group plans it applies to the adults.</summary>
    public int? BirthYearFrom { get; private set; }

    /// <summary>Youngest birth year the plan is for (inclusive).</summary>
    public int? BirthYearTo { get; private set; }

    /// <summary>The treasurer confirms the order before the member can pay (student, apprentice, ...).</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>Only for members who were never enrolled in an earlier season.</summary>
    public bool NewMembersOnly { get; private set; }

    /// <summary>Only when another adult of the member's family has a membership in the season (partner card, discount on children).</summary>
    public bool RequiresEnrolledFamilyAdult { get; private set; }

    /// <summary><c>false</c> for passive members: the payment is recorded, but nobody is enrolled in the season.</summary>
    public bool GrantsSeasonEnrollment { get; private set; } = true;

    public DateOnly? AvailableFrom { get; private set; }
    public DateOnly? AvailableUntil { get; private set; }

    /// <summary>Members see and choose the plan themselves. Switched off, only the treasurer can assign it.</summary>
    public bool IsSelfService { get; private set; } = true;

    public int SortOrder { get; private set; }

    /// <summary>One order covers several members of a family.</summary>
    public bool IsGroupPlan => MaxAdults > 1 || IncludesChildren;

    public bool IsFree => PriceCents == 0;

    public void Update(PlanData data)
    {
        var name = data.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new PreconditionException(ErrorCode.MembershipPlanNameRequired, "A name is required.");

        if (name.Length > MaxNameLength)
            throw new PreconditionException(ErrorCode.MembershipPlanNameTooLong, [MaxNameLength.ToString()], "The name is too long.");

        var description = string.IsNullOrWhiteSpace(data.Description) ? null : data.Description.Trim();
        if (description?.Length > MaxDescriptionLength)
            throw new PreconditionException(ErrorCode.MembershipPlanDescriptionTooLong, [MaxDescriptionLength.ToString()], "The description is too long.");

        if (data.PriceCents is < 0 or > MaxPriceCents)
            throw new PreconditionException(ErrorCode.MembershipPlanInvalidPrice, "The price is invalid.");

        if (data.MaxAdults is < 1 or > MaxAdultsLimit)
            throw new PreconditionException(ErrorCode.MembershipPlanInvalidMaxAdults, "A plan covers one or two adults.");

        if (data.BirthYearFrom is < MinBirthYear or > MaxBirthYear
         || data.BirthYearTo is < MinBirthYear or > MaxBirthYear
         || data.BirthYearFrom > data.BirthYearTo)
        {
            throw new PreconditionException(ErrorCode.MembershipPlanInvalidBirthYears, "The birth years are invalid.");
        }

        if (data.AvailableFrom > data.AvailableUntil)
            throw new PreconditionException(ErrorCode.MembershipPlanInvalidAvailability, "The plan must not end before it starts.");

        var isGroupPlan = data.MaxAdults > 1 || data.IncludesChildren;
        if (data.RequiresEnrolledFamilyAdult && isGroupPlan)
            throw new PreconditionException(ErrorCode.MembershipPlanFamilyDiscountOnlyForOneMember, "A plan that depends on the family is for one member.");

        Name = name;
        Description = description;
        PriceCents = data.PriceCents;
        MaxAdults = data.MaxAdults;
        IncludesChildren = data.IncludesChildren;
        BirthYearFrom = data.BirthYearFrom;
        BirthYearTo = data.BirthYearTo;
        RequiresApproval = data.RequiresApproval;
        NewMembersOnly = data.NewMembersOnly;
        RequiresEnrolledFamilyAdult = data.RequiresEnrolledFamilyAdult;
        GrantsSeasonEnrollment = data.GrantsSeasonEnrollment;
        AvailableFrom = data.AvailableFrom;
        AvailableUntil = data.AvailableUntil;
        IsSelfService = data.IsSelfService;
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public bool IsAvailableOn(DateOnly date) => (AvailableFrom is null || AvailableFrom <= date) && (AvailableUntil is null || date <= AvailableUntil);

    /// <summary>The data of this plan for another season whose year differs by <paramref name="yearOffset"/>: birth years and dates move with it.</summary>
    public PlanData CopyData(int yearOffset) => new(
        Name,
        Description,
        PriceCents,
        MaxAdults,
        IncludesChildren,
        BirthYearFrom + yearOffset,
        BirthYearTo + yearOffset,
        RequiresApproval,
        NewMembersOnly,
        RequiresEnrolledFamilyAdult,
        GrantsSeasonEnrollment,
        AvailableFrom?.AddYears(yearOffset),
        AvailableUntil?.AddYears(yearOffset),
        IsSelfService);
}
