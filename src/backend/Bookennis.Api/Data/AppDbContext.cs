using Bookennis.Domain.Weather;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Base;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.PlayRequests;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Families;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Domain.Notifications;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Data;

public class AppDbContext : IdentityDbContext<User, UserRole, int>
{
    private readonly IUserAccessor? userAccessor;
    private readonly ITenantService tenantService;
    private readonly IDomainEventDispatcher? domainEventDispatcher;
    private readonly IAuditContext? auditContext;

    public AppDbContext(string connectionString) : base(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options) => tenantService = new EmptyTenantService();

    // Without an audit context (design time, the audit writer itself) nothing is written to the audit log
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService? tenantService, IUserAccessor? userAccessor, IDomainEventDispatcher? domainEventDispatcher, IAuditContext? auditContext = null) : base(options)
    {
        this.domainEventDispatcher = domainEventDispatcher;
        this.auditContext = auditContext;
        this.userAccessor = userAccessor;
        this.tenantService = tenantService ?? new EmptyTenantService();
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingPlayer> BookingPlayers => Set<BookingPlayer>();
    public DbSet<RecurringBookingSeries> RecurringBookingSeries => Set<RecurringBookingSeries>();
    public DbSet<RecurringBookingSeriesPlayer> RecurringBookingSeriesPlayers => Set<RecurringBookingSeriesPlayer>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<ClubEmailTemplate> ClubEmailTemplates => Set<ClubEmailTemplate>();
    public DbSet<ClubApiKey> ClubApiKeys => Set<ClubApiKey>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<CourtBlocking> CourtBlockings => Set<CourtBlocking>();
    public DbSet<CourtBlockingCourt> CourtBlockingCourts => Set<CourtBlockingCourt>();
    public DbSet<CourtBlockingOccurrence> CourtBlockingOccurrences => Set<CourtBlockingOccurrence>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<ClubMember> ClubMembers => Set<ClubMember>();
    public DbSet<GuestMember> GuestMembers => Set<GuestMember>();
    public DbSet<PlayMode> PlayModes => Set<PlayMode>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<MemberSeason> MemberSeasons => Set<MemberSeason>();
    public DbSet<UserProfilePicture> UserProfilePictures => Set<UserProfilePicture>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<BadgeTier> BadgeTiers => Set<BadgeTier>();
    public DbSet<BadgeTierImage> BadgeTierImages => Set<BadgeTierImage>();
    public DbSet<MemberBadge> MemberBadges => Set<MemberBadge>();
    public DbSet<MemberBadgeSettings> MemberBadgeSettings => Set<MemberBadgeSettings>();
    public DbSet<OneTimeBadge> OneTimeBadges => Set<OneTimeBadge>();
    public DbSet<OneTimeBadgeImage> OneTimeBadgeImages => Set<OneTimeBadgeImage>();
    public DbSet<MemberOneTimeBadge> MemberOneTimeBadges => Set<MemberOneTimeBadge>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionPlanParticipant> SubscriptionPlanParticipants => Set<SubscriptionPlanParticipant>();
    public DbSet<SubscriptionPlanAssignment> SubscriptionPlanAssignments => Set<SubscriptionPlanAssignment>();
    public DbSet<ClubEvent> ClubEvents => Set<ClubEvent>();
    public DbSet<ClubEventQuestion> ClubEventQuestions => Set<ClubEventQuestion>();
    public DbSet<ClubEventQuestionOption> ClubEventQuestionOptions => Set<ClubEventQuestionOption>();
    public DbSet<ClubEventRegistration> ClubEventRegistrations => Set<ClubEventRegistration>();
    public DbSet<ClubEventRegistrationAnswer> ClubEventRegistrationAnswers => Set<ClubEventRegistrationAnswer>();
    public DbSet<ClubAnnouncement> ClubAnnouncements => Set<ClubAnnouncement>();
    public DbSet<ClubAnnouncementAttachment> ClubAnnouncementAttachments => Set<ClubAnnouncementAttachment>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MembershipOrder> MembershipOrders => Set<MembershipOrder>();
    public DbSet<MembershipOrderMember> MembershipOrderMembers => Set<MembershipOrderMember>();
    public DbSet<ClubPaymentAccount> ClubPaymentAccounts => Set<ClubPaymentAccount>();
    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();
    public DbSet<CourtSaleSettings> CourtSaleSettings => Set<CourtSaleSettings>();
    public DbSet<CourtPriceRule> CourtPriceRules => Set<CourtPriceRule>();
    public DbSet<CourtPurchase> CourtPurchases => Set<CourtPurchase>();
    public DbSet<BookingCreditGrant> BookingCreditGrants => Set<BookingCreditGrant>();
    public DbSet<ClubhouseItem> ClubhouseItems => Set<ClubhouseItem>();
    public DbSet<ClubhouseTab> ClubhouseTabs => Set<ClubhouseTab>();
    public DbSet<ClubhouseTabLine> ClubhouseTabLines => Set<ClubhouseTabLine>();
    public DbSet<ClubhouseTabPayment> ClubhouseTabPayments => Set<ClubhouseTabPayment>();
    public DbSet<ClubhouseSumUpAccount> ClubhouseSumUpAccounts => Set<ClubhouseSumUpAccount>();
    public DbSet<PlayRequest> PlayRequests => Set<PlayRequest>();
    public DbSet<PlayRequestInvitee> PlayRequestInvitees => Set<PlayRequestInvitee>();
    public DbSet<PlayerGroup> PlayerGroups => Set<PlayerGroup>();
    public DbSet<PlayerGroupMember> PlayerGroupMembers => Set<PlayerGroupMember>();
    public DbSet<AccountingSettings> AccountingSettings => Set<AccountingSettings>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<MoneyAccount> MoneyAccounts => Set<MoneyAccount>();
    public DbSet<AccountingCategory> AccountingCategories => Set<AccountingCategory>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<AccountingEntryLine> AccountingEntryLines => Set<AccountingEntryLine>();
    public DbSet<AccountingReceipt> AccountingReceipts => Set<AccountingReceipt>();
    public DbSet<AccountingSourceSetting> AccountingSourceSettings => Set<AccountingSourceSetting>();
    public DbSet<AccountingProposal> AccountingProposals => Set<AccountingProposal>();
    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<BankRule> BankRules => Set<BankRule>();
    public DbSet<BankCsvLayout> BankCsvLayouts => Set<BankCsvLayout>();
    public DbSet<InvoiceSettings> InvoiceSettings => Set<InvoiceSettings>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WeatherForecast> WeatherForecasts => Set<WeatherForecast>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("pg_trgm"); // the search of the audit log
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly, config => config.Namespace?.StartsWith(typeof(AppDbContext).Namespace ?? "") ?? false);
        DbContextHelpers.ApplyDefaultConventions(builder);
        builder.Entity<Club>().HasQueryFilter(i => tenantService.GetTenantId() == null || i.Id == tenantService.GetTenantId());
        builder.AddQueryFilterToAllEntitiesAssignableFrom<TenantDomainEntity>(i => tenantService.GetTenantId() == null || i.ClubId == tenantService.GetTenantId(), [typeof(GuestMember), typeof(ClubMember)]);

        base.OnModelCreating(builder);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        await DbContextHelpers.WrapSaveChanges(this, domainEventDispatcher, () => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken), userAccessor, cancellationToken, auditContext);

    // Necessary for LinqPad compatibility
    private sealed class EmptyTenantService : ITenantService
    {
        public bool TryGetTenantId(out int tenantId)
        {
            tenantId = -1;
            return false;
        }

        public int? GetTenantId() => null;

        public void SetTenantId(int tenantId) => throw new NotImplementedException();
    }
}