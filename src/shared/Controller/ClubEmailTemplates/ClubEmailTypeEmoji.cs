namespace Bookennis.Shared.Controller.ClubEmailTemplates;

/// <summary>The emoji of a club email type: shown in the template list of the app and in the header of the sent email.</summary>
public static class ClubEmailTypeEmoji
{
    public static string For(ClubEmailType type) => type switch
    {
        ClubEmailType.Welcome => "👋",
        ClubEmailType.SeasonActivated => "🎾",
        ClubEmailType.BadgeAwarded => "🏅",
        ClubEmailType.BookingDeleted => "🗓️",
        ClubEmailType.Announcement => "📣",
        ClubEmailType.BookingAdded => "🎾",
        ClubEmailType.BookingReminder => "⏰",
        ClubEmailType.ClubEventCreated => "📅",
        ClubEmailType.ClubEventReminder => "🔔",
        ClubEmailType.ClubEventRegistrationDeadline => "⏳",
        ClubEmailType.MembershipPaymentReceived => "💶",
        ClubEmailType.MembershipApproved => "✅",
        ClubEmailType.MembershipPaymentFailed => "⚠️",
        ClubEmailType.MembershipNeedsAttention => "🧾",
        ClubEmailType.CourtPurchaseConfirmed => "🎾",
        ClubEmailType.CourtPurchaseFailed => "⚠️",
        ClubEmailType.CourtPurchaseNeedsAttention => "🧾",
        ClubEmailType.CourtPurchaseCompensated => "🎟️",
        _ => "✉️"
    };
}
