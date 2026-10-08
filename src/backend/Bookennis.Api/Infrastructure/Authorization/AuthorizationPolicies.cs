namespace Bookennis.Api.Infrastructure.Authorization;

public static class AuthorizationPolicies
{
    // Application Policies
    public const string ApplicationAdministrator = nameof(ApplicationAdministrator);
    public const string ApplicationUser = nameof(ApplicationUser);
    /// <summary>A user signed in with their own credentials, not via a guest link. Required to change the email or password.</summary>
    public const string AccountOwner = nameof(AccountOwner);

    // Club Policies
    public const string ClubAdministrator = nameof(ClubAdministrator);
    public const string Member = nameof(Member);
    public const string ClubMember = nameof(ClubMember);
    public const string ClubTreasurer = nameof(ClubTreasurer);
    public const string OneTimeBadgeManager = nameof(OneTimeBadgeManager);
    /// <summary>Plans events in the club calendar: Maintainer, SportsDirector, YouthSportsDirector or Admin.</summary>
    public const string ClubEventManager = nameof(ClubEventManager);
    /// <summary>Writes announcements and sends them as email: SportsDirector, YouthSportsDirector or Admin.</summary>
    public const string ClubAnnouncementManager = nameof(ClubAnnouncementManager);
    /// <summary>Blocks courts for a time window (tournaments, maintenance, weather): Maintainer, SportsDirector or Admin.</summary>
    public const string CourtBlockingManager = nameof(CourtBlockingManager);

    //Booking Policies
    public const string OwnsBooking = nameof(OwnsBooking);
    public const string CanEditBooking = nameof(CanEditBooking);

    //Subscription Planner Policies
    public const string OwnsSubscriptionPlan = nameof(OwnsSubscriptionPlan);

    //PlayMode Policies
    public const string CanBookPlayMode = nameof(CanBookPlayMode);
}