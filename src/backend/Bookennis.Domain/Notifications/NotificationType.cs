namespace Bookennis.Domain.Notifications;

/// <summary>
/// What a user is notified about. For every type the user chooses the channels (push, email) in the profile.
/// Keep in sync with <c>Bookennis.Shared.Controller.Notifications.NotificationType</c>.
/// </summary>
public enum NotificationType
{
    BookingAdded = 0,
    BookingDeleted = 1,
    BookingReminder = 2,
    ClubEventCreated = 3,
    ClubEventReminder = 4,
    ClubEventRegistrationDeadline = 5,
    ClubAnnouncement = 6,
    BadgeAwarded = 7
}
