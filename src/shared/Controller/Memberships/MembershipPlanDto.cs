namespace Bookennis.Shared.Controller.Memberships;

public record GetMembershipPlansResult
{
    public required List<MembershipPlanDto> Plans { get; init; }
}

/// <summary>A line of the club's price list for one season.</summary>
public record MembershipPlanDto
{
    public required int Id { get; init; }
    public required int SeasonId { get; init; }
    public required string Name { get; init; }

    /// <summary>Plain text.</summary>
    public required string? Description { get; init; }

    /// <summary>The price of one order in cents, however many members it covers. 0 is a free plan.</summary>
    public required int PriceCents { get; init; }

    public required string Currency { get; init; }

    /// <summary>1 or 2. Together with <see cref="IncludesChildren"/>: 1 / false = one member, 2 / false = a couple, otherwise a family.</summary>
    public required int MaxAdults { get; init; }

    public required bool IncludesChildren { get; init; }

    /// <summary>Oldest birth year the plan is for (inclusive). For couples and families it applies to the adults.</summary>
    public required int? BirthYearFrom { get; init; }

    /// <summary>Youngest birth year the plan is for (inclusive).</summary>
    public required int? BirthYearTo { get; init; }

    /// <summary>The treasurer confirms the order before the member can pay.</summary>
    public required bool RequiresApproval { get; init; }

    public required bool NewMembersOnly { get; init; }

    /// <summary>Only when another adult of the member's family has a membership in the season.</summary>
    public required bool RequiresEnrolledFamilyAdult { get; init; }

    /// <summary><c>false</c> for passive members: paying does not enroll in the season.</summary>
    public required bool GrantsSeasonEnrollment { get; init; }

    public required DateOnly? AvailableFrom { get; init; }
    public required DateOnly? AvailableUntil { get; init; }

    /// <summary>Members choose the plan themselves. Switched off, only the treasurer assigns it.</summary>
    public required bool IsSelfService { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>The number of orders of the plan; only filled for treasurers. A plan with orders can't be deleted.</summary>
    public required int OrderCount { get; init; }

    public bool IsGroupPlan => MaxAdults > 1 || IncludesChildren;
    public bool IsFree => PriceCents == 0;
}

/// <summary>
/// Create and update model of a membership plan. Texts and numbers are validated by the domain,
/// so invalid values result in a localized error instead of a generic bad request.
/// </summary>
public record SaveMembershipPlanModel
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int PriceCents { get; init; }
    public int MaxAdults { get; init; } = 1;
    public bool IncludesChildren { get; init; }
    public int? BirthYearFrom { get; init; }
    public int? BirthYearTo { get; init; }
    public bool RequiresApproval { get; init; }
    public bool NewMembersOnly { get; init; }
    public bool RequiresEnrolledFamilyAdult { get; init; }
    public bool GrantsSeasonEnrollment { get; init; } = true;
    public DateOnly? AvailableFrom { get; init; }
    public DateOnly? AvailableUntil { get; init; }
    public bool IsSelfService { get; init; } = true;
}

/// <summary>Copies all plans of a season into another one. Birth years and dates move by the difference of the season years.</summary>
public record CopyMembershipPlansModel(int SourceSeasonId, int TargetSeasonId);

/// <summary>The ids of the plans of a season in the order members should see them.</summary>
public record SortMembershipPlansModel(int SeasonId, List<int> PlanIds);
