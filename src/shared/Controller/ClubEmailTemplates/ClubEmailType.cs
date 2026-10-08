namespace Bookennis.Shared.Controller.ClubEmailTemplates;

// Keep in sync with Bookennis.Domain.Clubs.EmailTemplates.ClubEmailType
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
