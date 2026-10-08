namespace Bookennis.Shared.Controller.Notifications;

// Keep in sync with Bookennis.Domain.Notifications.NotificationType
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

/// <summary>The channels the user is notified on for one type of notification.</summary>
public record NotificationPreferenceDto
{
    public NotificationType Type { get; init; }
    public bool Push { get; init; }
    public bool Email { get; init; }
}

public record GetNotificationPreferencesResult
{
    /// <summary>One entry for every notification type.</summary>
    public List<NotificationPreferenceDto> Preferences { get; init; } = [];
}

public record UpdateNotificationPreferencesModel
{
    /// <summary>Types that are missing keep their current channels.</summary>
    public List<NotificationPreferenceDto> Preferences { get; init; } = [];
}
