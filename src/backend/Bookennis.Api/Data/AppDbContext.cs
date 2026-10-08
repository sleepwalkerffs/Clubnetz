using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Base;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Families;
using Bookennis.Domain.Guests;
using Bookennis.Domain.Members;
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

    public AppDbContext(string connectionString) : base(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options) => tenantService = new EmptyTenantService();

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService? tenantService, IUserAccessor? userAccessor, IDomainEventDispatcher? domainEventDispatcher) : base(options)
    {
        this.domainEventDispatcher = domainEventDispatcher;
        this.userAccessor = userAccessor;
        this.tenantService = tenantService ?? new EmptyTenantService();
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingPlayer> BookingPlayers => Set<BookingPlayer>();
    public DbSet<RecurringBookingSeries> RecurringBookingSeries => Set<RecurringBookingSeries>();
    public DbSet<RecurringBookingSeriesPlayer> RecurringBookingSeriesPlayers => Set<RecurringBookingSeriesPlayer>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<ClubEmailTemplate> ClubEmailTemplates => Set<ClubEmailTemplate>();
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
    public DbSet<GuestCard> GuestCards => Set<GuestCard>();
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly, config => config.Namespace?.StartsWith(typeof(AppDbContext).Namespace ?? "") ?? false);
        DbContextHelpers.ApplyDefaultConventions(builder);
        builder.Entity<Club>().HasQueryFilter(i => tenantService.GetTenantId() == null || i.Id == tenantService.GetTenantId());
        builder.AddQueryFilterToAllEntitiesAssignableFrom<TenantDomainEntity>(i => tenantService.GetTenantId() == null || i.ClubId == tenantService.GetTenantId(), [typeof(GuestMember), typeof(ClubMember)]);

        base.OnModelCreating(builder);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        await DbContextHelpers.WrapSaveChanges(this, domainEventDispatcher, () => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken), userAccessor, cancellationToken);

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