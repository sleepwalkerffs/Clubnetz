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
    GuestCard = 3,
    BookingDeleted = 4,
    Announcement = 5,
    BookingAdded = 6,
    BookingReminder = 7,
    ClubEventCreated = 8,
    ClubEventReminder = 9,
    ClubEventRegistrationDeadline = 10
}
