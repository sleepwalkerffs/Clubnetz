using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Business.Push.DomainEventHandlers;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.Business.ClubAnnouncements;
using Bookennis.Api.Tests.Business.ClubEvents;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Base;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Bookings.Events;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.Members.Events;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Push;

public class PushDomainEventHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task NotifyBookingPlayers_ForwardsBookingAndSeriesToTheNotifier()
    {
        var notifier = Substitute.For<IBookingPushNotifier>();
        var emailNotifier = Substitute.For<IBookingEmailNotifier>();
        var handler = new NotifyBookingPlayersHandler(notifier, emailNotifier);

        await handler.Handle(new DomainEvent<BookingAddedDomainEvent>(typeof(Booking).GUID, 11, new BookingAddedDomainEvent()), CancellationToken.None);
        await handler.Handle(new DomainEvent<EntityCreated<RecurringBookingSeries>>(typeof(RecurringBookingSeries).GUID, 22, new EntityCreated<RecurringBookingSeries>()), CancellationToken.None);

        await notifier.Received(1).BookingAdded(11, Arg.Any<CancellationToken>());
        await notifier.Received(1).RecurringBookingAdded(22, Arg.Any<CancellationToken>());
        await emailNotifier.Received(1).BookingAdded(11, Arg.Any<CancellationToken>());
        await emailNotifier.Received(1).RecurringBookingAdded(22, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyClubEventCreated_NotifiesClubMembersButNotTheCreator()
    {
        var (eventId, otherMemberUserId) = await QueryAsync(async ctx =>
        {
            var (userId, _) = await MemberSeed.AddUserWithMember(ctx, "other@bookennis.com");
            await PushTestHelper.AddSubscription(ctx, userId, "https://fcm.googleapis.com/fcm/send/other");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");

            var clubEvent = await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(new DateOnly(2037, 10, 3)));
            return (clubEvent.Id, userId);
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new NotifyClubEventCreatedHandler(GetInstance<AppDbContext>(), new ClubEventNotifier(GetInstance<AppDbContext>(), pushService, Substitute.For<IMediator>()), IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId))
            .Handle(new DomainEvent<EntityCreated<ClubEvent>>(typeof(ClubEvent).GUID, eventId, new EntityCreated<ClubEvent>()), CancellationToken.None));

        var notification = pushService.Sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().BeEquivalentTo([TestDataSeed.UserId, otherMemberUserId]);
        notification.English.Title.Should().Be("New event: Work effort");
        notification.English.Body.Should().Be("Sat 3 October, 09:00 · Clubhouse · TestClub");
        notification.German.Title.Should().Be("Neue Veranstaltung: Work effort");
        notification.German.Body.Should().Be("Sa 3. Oktober, 09:00 · Clubhouse · TestClub");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/calendar/{eventId}");
    }

    [Fact]
    public async Task NotifyClubEventCreated_EventInThePast_NotifiesNobody()
    {
        var eventId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            return (await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3)))).Id;
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new NotifyClubEventCreatedHandler(GetInstance<AppDbContext>(), new ClubEventNotifier(GetInstance<AppDbContext>(), pushService, Substitute.For<IMediator>()), IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId))
            .Handle(new DomainEvent<EntityCreated<ClubEvent>>(typeof(ClubEvent).GUID, eventId, new EntityCreated<ClubEvent>()), CancellationToken.None));

        pushService.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyClubEventCreated_OrganizerDoesNotNotifyMembers_NotifiesNobody()
    {
        var eventId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            return (await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data() with { NotifyMembers = false })).Id;
        });

        var notifier = Substitute.For<IClubEventNotifier>();
        await ScopedAsync(() => new NotifyClubEventCreatedHandler(GetInstance<AppDbContext>(), notifier, IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId))
            .Handle(new DomainEvent<EntityCreated<ClubEvent>>(typeof(ClubEvent).GUID, eventId, new EntityCreated<ClubEvent>()), CancellationToken.None));

        notifier.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyClubAnnouncementCreated_NotifiesClubMembersButNotTheAuthor()
    {
        var announcementId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");
            return (await ClubAnnouncementSeed.Seed(ctx)).Id;
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new NotifyClubAnnouncementCreatedHandler(GetInstance<AppDbContext>(), pushService, IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId))
            .Handle(new DomainEvent<EntityCreated<ClubAnnouncement>>(typeof(ClubAnnouncement).GUID, announcementId, new EntityCreated<ClubAnnouncement>()), CancellationToken.None));

        var notification = pushService.Sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.UserId);
        notification.English.Title.Should().Be("News from TestClub");
        notification.English.Body.Should().Be("Summer party");
        notification.German.Title.Should().Be("Neuigkeiten von TestClub");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/news/{announcementId}");
    }

    [Fact]
    public async Task NotifyClubAnnouncementCreated_AlreadyExpired_NotifiesNobody()
    {
        var announcementId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            return (await ClubAnnouncementSeed.Seed(ctx, ClubAnnouncementSeed.Data(expiresOn: ClubAnnouncementSeed.Today.AddDays(-1)))).Id;
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new NotifyClubAnnouncementCreatedHandler(GetInstance<AppDbContext>(), pushService, IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId))
            .Handle(new DomainEvent<EntityCreated<ClubAnnouncement>>(typeof(ClubAnnouncement).GUID, announcementId, new EntityCreated<ClubAnnouncement>()), CancellationToken.None));

        pushService.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyBadgeAwarded_TierBadge_NotifiesTheMember()
    {
        var (tierId, seasonId) = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            var seasonId = await AddSeason(ctx);
            var tier = new BadgeTier(TestDataSeed.ClubId, seasonId, 1, "Bronze", "Bronze badge", 3, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();
            return (tier.Id, seasonId);
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new NotifyBadgeAwardedHandler(GetInstance<AppDbContext>(), pushService)
            .Handle(new DomainEvent<MemberBadgeAwardedDomainEvent>(typeof(MemberBadge).GUID, 0, new MemberBadgeAwardedDomainEvent(TestDataSeed.Member1Id, tierId, seasonId)), CancellationToken.None));

        var notification = pushService.Sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.UserId);
        notification.English.Body.Should().Be("You earned the badge \"Bronze\".");
        notification.German.Body.Should().Be("Du hast das Abzeichen „Bronze“ erhalten.");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/members/{TestDataSeed.Member1Id}/trophy-case");
    }

    [Fact]
    public async Task NotifyBadgeAwarded_OneTimeBadgeOfChild_NotifiesTheParentAndNamesTheChild()
    {
        var (badgeId, childMemberId) = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            var (_, childMemberId) = await MemberSeed.AddChild(ctx, TestDataSeed.UserId, "Kid");
            var badge = new OneTimeBadge(TestDataSeed.ClubId, await AddSeason(ctx), "Club champion", "Won the club championship");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();
            return (badge.Id, childMemberId);
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new NotifyBadgeAwardedHandler(GetInstance<AppDbContext>(), pushService)
            .Handle(new DomainEvent<MemberOneTimeBadgeAwardedDomainEvent>(typeof(MemberOneTimeBadge).GUID, 0, new MemberOneTimeBadgeAwardedDomainEvent(childMemberId, badgeId)), CancellationToken.None));

        var notification = pushService.Sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.UserId);
        notification.English.Body.Should().Be("Kid earned the badge \"Club champion\".");
        notification.German.Body.Should().Be("Kid hat das Abzeichen „Club champion“ erhalten.");
    }

    private static async Task<int> AddSeason(AppDbContext ctx)
    {
        var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
        ctx.Add(season);
        await ctx.SaveChangesAsync();
        return season.Id;
    }
}
