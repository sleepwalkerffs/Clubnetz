namespace Bookennis.Shared.Controller.Memberships;

/// <summary>
/// The treasurer's view of a season: every member of the club and every membership order.
/// It is the only list of members a treasurer gets, so it carries names, plan and payment and nothing else about a person.
/// </summary>
public record GetMembershipPaymentsResult
{
    public required int SeasonId { get; init; }
    public required List<MembershipPaymentMemberDto> Members { get; init; }

    /// <summary>The orders of the season, newest first. With a status filter only the orders in that status.</summary>
    public required List<MembershipPaymentOrderDto> Orders { get; init; }
}

public record MembershipPaymentMemberDto
{
    public required int MemberId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    /// <summary>The family of the member, to record a payment for a couple or a family.</summary>
    public required int? FamilyId { get; init; }

    public required bool IsChild { get; init; }
    public required MembershipEnrollmentSource Enrollment { get; init; }
}

/// <summary>How a member got into the season.</summary>
public enum MembershipEnrollmentSource
{
    NotEnrolled = 0,

    /// <summary>Enrolled in the member administration, without an order.</summary>
    ByHand = 1,

    /// <summary>Enrolled by an order that was paid online.</summary>
    PaidOnline = 2,

    /// <summary>Enrolled by an order the treasurer marked as paid, or by a free plan.</summary>
    MarkedAsPaid = 3,
}

public record MembershipPaymentOrderDto
{
    public required int Id { get; init; }
    public required int MembershipPlanId { get; init; }
    public required string PlanName { get; init; }
    public required int AmountCents { get; init; }
    public required string Currency { get; init; }
    public required MembershipOrderStatus Status { get; init; }
    public required MembershipPaymentMethod? PaymentMethod { get; init; }
    public required int? PayerMemberId { get; init; }
    public required List<int> MemberIds { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset? PaidAt { get; init; }

    /// <summary>Written by the treasurers, never shown to the member.</summary>
    public required string? Note { get; init; }
}

public record GetMembershipPaymentsSummaryResult
{
    /// <summary>The sum of the paid orders in cents.</summary>
    public required long ReceivedCents { get; init; }

    /// <summary>The sum of the orders with a debit on its way.</summary>
    public required long ProcessingCents { get; init; }

    /// <summary>The sum of the orders that wait for a confirmation or a payment, or whose payment failed.</summary>
    public required long OpenCents { get; init; }

    public required string Currency { get; init; }

    /// <summary>Members covered by an order that is paid or being paid.</summary>
    public required int MembersPaid { get; init; }

    public required int MembersEnrolled { get; init; }
    public required int MembersTotal { get; init; }

    /// <summary>The number of orders per status.</summary>
    public required Dictionary<MembershipOrderStatus, int> OrdersByStatus { get; init; }
}

/// <summary>A fee the treasurer received outside of Clubnetz (bank transfer, cash): creates a paid order and enrolls the members.</summary>
public record RecordMembershipPaymentModel
{
    public int MembershipPlanId { get; init; }
    public List<int> MemberIds { get; init; } = [];

    /// <summary>Who paid; one of the members by default.</summary>
    public int? PayerMemberId { get; init; }

    public MembershipPaymentMethod Method { get; init; } = MembershipPaymentMethod.BankTransfer;
    public DateOnly PaidOn { get; init; }
    public string? Note { get; init; }
}

public record ApproveMembershipOrderModel
{
    /// <summary>Who the order covers from now on; <c>null</c> leaves the members as the member chose them.</summary>
    public List<int>? MemberIds { get; init; }
}

public record MarkMembershipOrderPaidModel
{
    /// <summary>Who the order covers from now on; <c>null</c> leaves the members as the member chose them.</summary>
    public List<int>? MemberIds { get; init; }

    public MembershipPaymentMethod Method { get; init; } = MembershipPaymentMethod.BankTransfer;
    public DateOnly PaidOn { get; init; }
    public string? Note { get; init; }
}
