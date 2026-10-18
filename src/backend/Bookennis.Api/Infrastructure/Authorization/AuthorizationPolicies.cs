namespace Bookennis.Api.Infrastructure.Authorization;

public static class AuthorizationPolicies
{
    // Application Policies
    public const string ApplicationAdministrator = nameof(ApplicationAdministrator);
    public const string ApplicationUser = nameof(ApplicationUser);
    /// <summary>A signed-in user (cookie, never an API key). Required to change the email or password.</summary>
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
    /// <summary>Connects and disconnects the Stripe account of the club: a signed-in Admin. Not accepted for requests made with an API key.</summary>
    public const string ClubPaymentAccountManager = nameof(ClubPaymentAccountManager);
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
    /// <summary>
    /// An active member of the club (<see cref="Business.Members.ActiveMembership"/>: enrolled in the active season, parent of an enrolled
    /// child, or a staff role) or an application administrator. Required for everything that shows other members.
    /// </summary>
    public const string ActiveMember = nameof(ActiveMember);

    // Court sales: signed in with a cookie, never with an API key
    /// <summary>Any member of the club, signed in (not with an API key): sees the prices and buys court time.</summary>
    public const string CourtSalesMember = nameof(CourtSalesMember);
    /// <summary>Treasurer or Admin, signed in (not with an API key): prices, sales and credits.</summary>
    public const string CourtSalesManager = nameof(CourtSalesManager);
    /// <summary>Resource: CourtPurchaseAuthorizationModel. The buyer, or a Treasurer or Admin of the club (not for releasing a hold).</summary>
    public const string OwnsCourtPurchase = nameof(OwnsCourtPurchase);

    // Clubhouse: signed in with a cookie, never with an API key (money of the host)
    /// <summary>Any member of the club, signed in (not with an API key): reads the own tabs and those of the children of their families.</summary>
    public const string ClubhouseGuest = nameof(ClubhouseGuest);
    /// <summary>ClubhouseHost or Admin, signed in (not with an API key): the menu and the tabs of all guests.</summary>
    public const string ClubhouseHost = nameof(ClubhouseHost);
    /// <summary>Resource: ClubhouseTabAuthorizationModel. The guest of the tab or a parent of their family, signed in (not with an API key): pays it in the app.</summary>
    public const string PaysClubhouseTab = nameof(PaysClubhouseTab);

    //Booking Policies
    public const string OwnsBooking = nameof(OwnsBooking);
    public const string CanEditBooking = nameof(CanEditBooking);

    //Family Policies (resource: FamilyAuthorizationModel)
    /// <summary>Reads a family: one of its members, or an Admin of the club.</summary>
    public const string FamilyMember = nameof(FamilyMember);
    /// <summary>Changes a family: one of its parents, an Admin of the club, or a member who takes themself out of it.</summary>
    public const string FamilyParent = nameof(FamilyParent);

    //Membership Policies (resource: MembershipOrderAuthorizationModel)
    /// <summary>Reads, pays or withdraws a membership order: its payer, a member it covers, a parent of a covered child, or a Treasurer or Admin of the club.</summary>
    public const string OwnsMembershipOrder = nameof(OwnsMembershipOrder);

    //Subscription Planner Policies
    public const string OwnsSubscriptionPlan = nameof(OwnsSubscriptionPlan);

    //PlayMode Policies
    public const string CanBookPlayMode = nameof(CanBookPlayMode);
}