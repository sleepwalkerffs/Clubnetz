using Bookennis.Shared.Controller.ClubEmailTemplates;

namespace Bookennis.Client.Pages.ClubShell.Club.EmailTemplates;

public static class EmailTypeInfo
{
    public static string Emoji(ClubEmailType type) => type switch
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

    public static string NameKey(ClubEmailType type) => $"Type_{type}";

    public static string DescriptionKey(ClubEmailType type) => $"TypeDescription_{type}";
}
