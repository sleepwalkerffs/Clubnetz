namespace Bookennis.Shared.Controller.ClubAnnouncements;

// Keep in sync with Bookennis.Domain.ClubAnnouncements.ClubAnnouncementAudience
public enum ClubAnnouncementAudience
{
    AllMembers = 0,

    /// <summary>Members enrolled in the season that is active today.</summary>
    ActiveSeasonMembers = 1,

    /// <summary>Members younger than 18. Children without an own email are reached via a parent.</summary>
    Youth = 2,

    /// <summary>Members with at least one of the selected roles.</summary>
    Roles = 3,
}
