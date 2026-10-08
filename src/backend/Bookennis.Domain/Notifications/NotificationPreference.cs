using Bookennis.Domain.Base;

namespace Bookennis.Domain.Notifications;

/// <summary>
/// The channels a user wants to be notified on for one <see cref="NotificationType"/>.
/// A user without a preference for a type gets the defaults (<see cref="DefaultPush"/>, <see cref="DefaultEmail"/>).
/// </summary>
public class NotificationPreference : DomainEntity
{
    // Queries that resolve recipients only look for rows that switch a channel off, so both defaults have to stay "on".
    public const bool DefaultPush = true;
    public const bool DefaultEmail = true;

    private NotificationPreference() { }

    public NotificationPreference(int userId, NotificationType type, bool push, bool email)
    {
        UserId = userId;
        Type = type;
        Push = push;
        Email = email;
    }

    public int UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public bool Push { get; private set; }
    public bool Email { get; private set; }

    public void Update(bool push, bool email)
    {
        Push = push;
        Email = email;
    }
}
