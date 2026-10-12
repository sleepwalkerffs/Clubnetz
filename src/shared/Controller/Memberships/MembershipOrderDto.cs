namespace Bookennis.Shared.Controller.Memberships;

/// <summary>Keep in sync with <c>Bookennis.Domain.Memberships.MembershipOrderStatus</c>.</summary>
public enum MembershipOrderStatus
{
    /// <summary>The treasurer has to confirm that the member may have the plan.</summary>
    AwaitingApproval = 0,
    AwaitingPayment = 1,

    /// <summary>A SEPA direct debit was started and has not settled yet. The members are already enrolled.</summary>
    Processing = 2,
    Paid = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6,
    Disputed = 7,
}

/// <summary>Keep in sync with <c>Bookennis.Domain.Memberships.MembershipPaymentMethod</c>.</summary>
public enum MembershipPaymentMethod
{
    /// <summary>Paid online with Stripe.</summary>
    Online = 0,
    BankTransfer = 1,
    Cash = 2,
    Other = 3,
}

/// <summary>What a member can choose for a season: the plans, for whom, and the orders that already exist.</summary>
public record GetMembershipOptionsResult
{
    /// <summary><c>null</c> when the club has no running or upcoming season; the lists are empty then.</summary>
    public required int? SeasonId { get; init; }

    public required DateOnly? SeasonStart { get; init; }
    public required DateOnly? SeasonEnd { get; init; }

    /// <summary>The club takes online payments. Otherwise the fee is paid to the club directly and the treasurer records it.</summary>
    public required bool OnlinePaymentAvailable { get; init; }

    /// <summary>The caller and, for a parent, the members of the family: the people an order can cover.</summary>
    public required List<MembershipPersonDto> People { get; init; }

    public required List<MembershipPlanOptionDto> Plans { get; init; }

    /// <summary>The orders of the season that cover or were paid by one of the <see cref="People"/>, newest first.</summary>
    public required List<MembershipOrderDto> Orders { get; init; }
}

public record MembershipPersonDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required bool IsMe { get; init; }
    public required bool IsChild { get; init; }

    /// <summary>Enrolled in the season, by an order or by hand.</summary>
    public required bool IsEnrolled { get; init; }

    /// <summary>Covered by an order of the season that is not cancelled, failed or refunded.</summary>
    public required bool HasMembership { get; init; }
}

public record MembershipPlanOptionDto
{
    public required MembershipPlanDto Plan { get; init; }

    /// <summary>
    /// For whom the plan could be ordered: one choice per person for a plan for one member,
    /// one choice with the whole family for a couple or family plan.
    /// </summary>
    public required List<MembershipPlanChoiceDto> Choices { get; init; }

    public bool IsEligible => Choices.Any(c => c.IsEligible);
}

public record MembershipPlanChoiceDto
{
    public required List<int> MemberIds { get; init; }
    public required bool IsEligible { get; init; }

    /// <summary>Why the plan can't be chosen (error codes, e.g. <c>BirthYearOutOfRange</c>); empty when it can.</summary>
    public required List<string> Reasons { get; init; }
}

public record MembershipOrderDto
{
    public required int Id { get; init; }
    public required int SeasonId { get; init; }
    public required int MembershipPlanId { get; init; }
    public required string PlanName { get; init; }
    public required int AmountCents { get; init; }
    public required string Currency { get; init; }
    public required MembershipOrderStatus Status { get; init; }
    public required MembershipPaymentMethod? PaymentMethod { get; init; }
    public required int? PayerMemberId { get; init; }
    public required string? PayerName { get; init; }
    public required List<MembershipOrderMemberDto> Members { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset? ApprovedAt { get; init; }
    public required DateTimeOffset? PaidAt { get; init; }

    /// <summary><c>false</c> for passive memberships: paying does not enroll in the season.</summary>
    public required bool GrantsSeasonEnrollment { get; init; }

    /// <summary>The order waits for its payment (or the last attempt failed) and can be paid or, when free, confirmed.</summary>
    public required bool CanPay { get; init; }

    /// <summary>Nothing was paid yet, so the member can withdraw the order.</summary>
    public required bool CanCancel { get; init; }

    public bool IsFree => AmountCents == 0;
}

public record MembershipOrderMemberDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}

public record CreateMembershipOrderModel
{
    public int MembershipPlanId { get; init; }

    /// <summary>The members the order covers: the caller, or members of the family the caller is a parent of.</summary>
    public List<int> MemberIds { get; init; } = [];
}

/// <summary>The address of the payment page the member is sent to.</summary>
public record StartMembershipCheckoutResult(string Url);
