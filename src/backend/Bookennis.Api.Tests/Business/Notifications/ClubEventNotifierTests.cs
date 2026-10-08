using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.Business.ClubEvents;
using Bookennis.Api.Tests.Business.Push;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Notifications;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Notifications;

public class ClubEventNotifierTests(TestFixture fixture) : TestBase(fixture)
{
    private const string UserEmail = "user@bookennis.com";
    private const string AdminEmail = "admin@bookennis.com";

    [Fact]
    public async Task EventCreated_NotifiesClubMembersByPushAndEmailButNotTheCreator()
    {
        var eventId = await QueryAsync(async ctx =>
        {
            await AddSubscriptions(ctx);
            return (await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(new DateOnly(2037, 10, 3)))).Id;
        });

        var (push, batches) = await Run(notifier => notifier.EventCreated(eventId, TestDataSeed.AdminId, CancellationToken.None));

        var notification = push.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.UserId);
        notification.English.Title.Should().Be("New event: Work effort");
        notification.German.Body.Should().Be("Sa 3. Oktober, 09:00 · Clubhouse · TestClub");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/calendar/{eventId}");

        var batch = batches.Should().ContainSingle().Subject;
        batch.Should().BeEquivalentTo(new { ClubId = TestDataSeed.ClubId, ClubEventId = eventId, Type = ClubEmailType.ClubEventCreated }, o => o.ExcludingMissingMembers());
        batch.Recipients.Select(r => r.Email).Should().Equal(UserEmail);
    }

    [Fact]
    public async Task EventCreated_RespectsTheChannelsOfTheMembers()
    {
        var eventId = await QueryAsync(async ctx =>
        {
            await AddSubscriptions(ctx);
            ctx.Add(new NotificationPreference(TestDataSeed.UserId, NotificationType.ClubEventCreated, push: false, email: false));
            return (await ClubEventSeed.SeedEvent(ctx)).Id;
        });

        var (push, batches) = await Run(notifier => notifier.EventCreated(eventId, null, CancellationToken.None));

        push.Should().ContainSingle().Which.UserIds.Should().Equal(TestDataSeed.AdminId);
        batches.Should().ContainSingle().Which.Recipients.Select(r => r.Email).Should().Equal(AdminEmail);
    }

    [Fact]
    public async Task EventReminder_NotifiesTheRegisteredMembers()
    {
        var eventId = await QueryAsync(async ctx =>
        {
            await AddSubscriptions(ctx);
            var clubEvent = await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(withQuestions: false));
            clubEvent.Register(TestDataSeed.Member1Id, 2, [], null, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
            return clubEvent.Id;
        });

        var (push, batches) = await Run(notifier => notifier.EventReminder(eventId, CancellationToken.None));

        var notification = push.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.UserId);
        notification.English.Title.Should().Be("Reminder: Work effort");
        notification.German.Title.Should().Be("Erinnerung: Work effort");

        var batch = batches.Should().ContainSingle().Subject;
        batch.Type.Should().Be(ClubEmailType.ClubEventReminder);
        batch.Recipients.Select(r => r.Email).Should().Equal(UserEmail);
    }

    [Fact]
    public async Task RegistrationDeadlineReminder_NotifiesTheMembersWhoHaveNotRegistered()
    {
        var eventId = await QueryAsync(async ctx =>
        {
            await AddSubscriptions(ctx);
            var clubEvent = await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(withQuestions: false) with { RegistrationDeadline = DateTimeOffset.UtcNow.AddHours(12) });
            clubEvent.Register(TestDataSeed.Member1Id, 1, [], null, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
            return clubEvent.Id;
        });

        var (push, batches) = await Run(notifier => notifier.RegistrationDeadlineReminder(eventId, CancellationToken.None));

        var notification = push.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.AdminId);
        notification.English.Title.Should().Be("Registration closes soon: Work effort");
        notification.English.Body.Should().StartWith("Register by ").And.EndWith(" · TestClub");
        notification.German.Title.Should().Be("Anmeldeschluss naht: Work effort");

        var batch = batches.Should().ContainSingle().Subject;
        batch.Type.Should().Be(ClubEmailType.ClubEventRegistrationDeadline);
        batch.Recipients.Select(r => r.Email).Should().Equal(AdminEmail);
    }

    [Fact]
    public async Task SendClubEventEmailBatch_SendsTheClubEmailWithTheEventDetails()
    {
        var eventId = await QueryAsync(async ctx => (await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(new DateOnly(2037, 10, 3)) with
        {
            RegistrationDeadline = new DateTimeOffset(2037, 10, 1, 16, 0, 0, TimeSpan.Zero)
        })).Id);

        var mediator = Substitute.For<IMediator>();
        EmailRecipient[] recipients =
        [
            new(TestDataSeed.Member1Id, TestDataSeed.UserId, UserEmail, "Us", "Er", Language.German),
            new(TestDataSeed.Member2Id, TestDataSeed.AdminId, AdminEmail, "Ad", "Min", Language.English)
        ];

        await ScopedAsync(() => new SendClubEventEmailBatch.Handler(GetInstance<AppDbContext>(), mediator, new AppSettings())
            .Handle(new SendClubEventEmailBatch(TestDataSeed.ClubId, eventId, ClubEmailType.ClubEventReminder, recipients), CancellationToken.None));

        var emails = mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEmail>().ToList();
        emails.Select(e => e.Recipient).Should().Equal(UserEmail, AdminEmail);
        emails.Should().OnlyContain(e => e.ClubId == TestDataSeed.ClubId && e.Type == ClubEmailType.ClubEventReminder);

        var german = emails[0].Variables.Should().BeOfType<ClubEventEmailVariables>().Subject;
        german.Member.FirstName.Should().Be("Us");
        german.Event.Title.Should().Be("Work effort");
        german.Event.Date.Should().Be("Samstag, 3. Oktober 2037");
        german.Event.Time.Should().Be("09:00 – 13:00");
        german.Event.Location.Should().Be("Clubhouse");
        // The deadline is shown in the time zone of the clubs (16:00 UTC is 18:00 in Vienna in summer)
        german.Event.RegistrationDeadline.Should().Be("Donnerstag, 1. Oktober 2037, 18:00");
        german.Event.Url.Should().EndWith($"/clubs/{TestDataSeed.ClubId}/calendar/{eventId}");
    }

    [Fact]
    public async Task SendClubEventEmailBatch_EventOfAnotherClubOrDeleted_SendsNothing()
    {
        var eventId = await QueryAsync(async ctx => (await ClubEventSeed.SeedEvent(ctx)).Id);

        var mediator = Substitute.For<IMediator>();
        EmailRecipient[] recipients = [new(TestDataSeed.Member1Id, TestDataSeed.UserId, UserEmail, "Us", "Er", Language.German)];

        await ScopedAsync(async () =>
        {
            var handler = new SendClubEventEmailBatch.Handler(GetInstance<AppDbContext>(), mediator, new AppSettings());
            await handler.Handle(new SendClubEventEmailBatch(TestDataSeed.ClubId + 1, eventId, ClubEmailType.ClubEventCreated, recipients), CancellationToken.None);
            await handler.Handle(new SendClubEventEmailBatch(TestDataSeed.ClubId, eventId + 1000, ClubEmailType.ClubEventCreated, recipients), CancellationToken.None);
        });

        mediator.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void ClubEvent_Update_MovedEventOrDeadline_GetsANewReminder()
    {
        var data = ClubEventSeed.Data(withQuestions: false) with { RegistrationDeadline = DateTimeOffset.UtcNow.AddDays(5) };
        var clubEvent = new ClubEvent(1, null, data);
        clubEvent.MarkReminderSent();
        clubEvent.MarkDeadlineReminderSent();

        clubEvent.Update(data with { Title = "Renamed" });
        clubEvent.ReminderSentAt.Should().NotBeNull();
        clubEvent.DeadlineReminderSentAt.Should().NotBeNull();

        clubEvent.Update(data with { StartDate = data.StartDate.AddDays(1) });
        clubEvent.ReminderSentAt.Should().BeNull();
        clubEvent.DeadlineReminderSentAt.Should().NotBeNull();

        clubEvent.Update(data with { StartDate = data.StartDate.AddDays(1), RegistrationDeadline = DateTimeOffset.UtcNow.AddDays(6) });
        clubEvent.DeadlineReminderSentAt.Should().BeNull();
    }

    private static async Task AddSubscriptions(AppDbContext ctx)
    {
        await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
        await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");
    }

    private async Task<(List<RecordingPushNotificationService.SentNotification> Push, List<SendClubEventEmailBatch> EmailBatches)> Run(Func<ClubEventNotifier, Task> act)
    {
        var pushService = new RecordingPushNotificationService();
        var mediator = Substitute.For<IMediator>();

        await ScopedAsync(() => act(new ClubEventNotifier(GetInstance<AppDbContext>(), pushService, mediator)));

        return (pushService.Sent, mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEventEmailBatch>().ToList());
    }
}
