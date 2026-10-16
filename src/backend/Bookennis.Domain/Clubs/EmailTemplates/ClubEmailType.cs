namespace Bookennis.Domain.Clubs.EmailTemplates;

/// <summary>
/// Emails that are triggered by something happening in a club and can be customized by the club.
/// Keep in sync with <c>Bookennis.Shared.Controller.ClubEmailTemplates.ClubEmailType</c>.
/// </summary>
public enum ClubEmailType
{
    Welcome = 0,
    SeasonActivated = 1,
    BadgeAwarded = 2,
    // 3 was GuestCard (the removed guest system). The number is not used again.
    BookingDeleted = 4,
    Announcement = 5,
    BookingAdded = 6,
    BookingReminder = 7,
    ClubEventCreated = 8,
    ClubEventReminder = 9,
    ClubEventRegistrationDeadline = 10,
    MembershipPaymentReceived = 11,
    MembershipApproved = 12,
    MembershipPaymentFailed = 13,
    MembershipNeedsAttention = 14,
    CourtPurchaseConfirmed = 15,
    CourtPurchaseFailed = 16,
    CourtPurchaseNeedsAttention = 17,
    CourtPurchaseCompensated = 18,
}
