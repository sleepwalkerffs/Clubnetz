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
    /// <summary>Creates and revokes the API keys of the club: a signed-in Admin. Not accepted for requests made with an API key.</summary>
    public const string ClubApiKeyManager = nameof(ClubApiKeyManager);
    /// <summary>Everybody who has a member in the club, whatever its roles are. For endpoints that check the rest themselves.</summary>
    public const string AnyMember = nameof(AnyMember);
    public const string Member = nameof(Member);
    public const string ClubMember = nameof(ClubMember);
    public const string ClubTreasurer = nameof(ClubTreasurer);
    public const string OneTimeBadgeManager = nameof(OneTimeBadgeManager);
    /// <summary>Plans events in the club calendar: Maintainer, SportsDirector, YouthSportsDirector or Admin.</summary>
    public const string ClubEventManager = nameof(ClubEventManager);
    /// <summary>Writes announcements and sends them as email: Maintainer, SportsDirector, YouthSportsDirector or Admin.</summary>
    public const string ClubAnnouncementManager = nameof(ClubAnnouncementManager);
    /// <summary>Blocks courts for a time window (tournaments, maintenance, weather): Maintainer, SportsDirector or Admin.</summary>
    public const string CourtBlockingManager = nameof(CourtBlockingManager);
    /// <summary>Reads the members list and the details of every member, without editing them: Maintainer or Admin.</summary>
    public const string ClubMemberViewer = nameof(ClubMemberViewer);
    /// <summary>Reads the statistics of the whole club: Maintainer or Admin.</summary>
    public const string ClubStatisticsViewer = nameof(ClubStatisticsViewer);

    //Booking Policies
    public const string OwnsBooking = nameof(OwnsBooking);
    public const string CanEditBooking = nameof(CanEditBooking);

    //Subscription Planner Policies
    public const string OwnsSubscriptionPlan = nameof(OwnsSubscriptionPlan);

    //PlayMode Policies
    public const string CanBookPlayMode = nameof(CanBookPlayMode);
}