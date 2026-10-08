namespace Bookennis.Api.Business.ClubEmails;

// The variables that club admins can use in their email templates. Every public property is exposed
// to the Liquid template in snake_case (e.g. Member.FirstName -> {{ member.first_name }}).
// Only put values here that the recipient of the email is allowed to see - never whole entities.

public record MemberVariables(string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public record ClubVariables(string Name, string? WebsiteUrl, string? ReplyToEmail);

public record SeasonVariables(string From, string To)
{
    public string Period => $"{From} - {To}";
}

public record BadgeVariables(string Name, string Description, string? ImageUrl, bool IsOneTime);

public record BookingVariables(string Date, string Court, string DeletedBy, string Reason);

/// <summary>A booking the member plays in. <c>Date</c> describes the repetition for a recurring booking.</summary>
public record BookingDetailsVariables(string Date, string Time, string Court, string PlayMode, string Players, string Url);

/// <summary>A club event. <c>Time</c> is empty for all-day events.</summary>
public record EventVariables(string Title, string Date, string? Time, string? Location, string? RegistrationDeadline, string Url);

/// <summary>
/// Markdown that is inserted into the email body as it is, e.g. the text of an announcement.
/// Plain strings are escaped instead, so only use this for text written by someone who may format the email.
/// </summary>
public readonly record struct MarkdownText(string Value)
{
    public override string ToString() => Value;
}

public record AnnouncementVariables(string Title, MarkdownText Body, string Url);

public abstract record ClubEmailVariables(MemberVariables Member)
{
    /// <summary>Filled in by <see cref="SendClubEmail"/> from the club the email is sent for.</summary>
    public ClubVariables Club { get; init; } = new(string.Empty, null, null);
}

public record WelcomeEmailVariables(MemberVariables Member, SeasonVariables Season) : ClubEmailVariables(Member);

public record SeasonActivatedEmailVariables(MemberVariables Member, SeasonVariables Season) : ClubEmailVariables(Member);

public record BadgeAwardedEmailVariables(MemberVariables Member, BadgeVariables Badge, string TrophyCaseUrl) : ClubEmailVariables(Member);

public record GuestCardEmailVariables(MemberVariables Member, string GuestCardUrl) : ClubEmailVariables(Member);

public record BookingDeletedEmailVariables(MemberVariables Member, BookingVariables Booking) : ClubEmailVariables(Member);

public record AnnouncementEmailVariables(MemberVariables Member, AnnouncementVariables Announcement) : ClubEmailVariables(Member);

public record BookingAddedEmailVariables(MemberVariables Member, BookingDetailsVariables Booking, string BookedBy) : ClubEmailVariables(Member);

public record BookingReminderEmailVariables(MemberVariables Member, BookingDetailsVariables Booking) : ClubEmailVariables(Member);

/// <summary>Used by all emails about a club event (published, reminder, registration deadline).</summary>
public record ClubEventEmailVariables(MemberVariables Member, EventVariables Event) : ClubEmailVariables(Member);
