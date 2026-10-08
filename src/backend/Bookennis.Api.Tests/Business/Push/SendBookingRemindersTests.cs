using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Notifications;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Push;

public class SendBookingRemindersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task SendBookingReminders_BookingStartsSoon_RemindsAllPlayersOnce()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");

            var id = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddMinutes(90), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            await PushTestHelper.BackdateBooking(ctx, id);
            return id;
        });

        var firstRun = await Run();
        var secondRun = await Run();

        var notification = firstRun.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.AdminId, TestDataSeed.UserId);
        notification.English.Title.Should().StartWith("Your booking starts at ");
        notification.German.Title.Should().StartWith("Deine Buchung beginnt um ");
        notification.German.Body.Should().Be("Court 1 · TestClub");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/booking/{bookingId}");

        secondRun.Should().BeEmpty();
        (await QueryAsync(ctx => ctx.Bookings.SingleAsync(b => b.Id == bookingId))).ReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendBookingReminders_BookedOnShortNotice_SendsNoReminder()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            return await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddMinutes(30), TestDataSeed.Member1Id);
        });

        var sent = await Run();

        sent.Should().BeEmpty();
        (await QueryAsync(ctx => ctx.Bookings.SingleAsync(b => b.Id == bookingId))).ReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendBookingReminders_BookingLaterOrAlreadyStarted_IsLeftAlone()
    {
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId);
            var later = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddHours(5), TestDataSeed.Member1Id);
            var started = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddMinutes(-10), TestDataSeed.Member1Id);
            await PushTestHelper.BackdateBooking(ctx, later);
            await PushTestHelper.BackdateBooking(ctx, started);
        });

        var sent = await Run();

        sent.Should().BeEmpty();
        (await QueryAsync(ctx => ctx.Bookings.Where(b => b.ReminderSentAt != null).CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task SendBookingReminders_SendsTheReminderEmailToThePlayersWhoWantIt()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            ctx.Add(new NotificationPreference(TestDataSeed.AdminId, NotificationType.BookingReminder, push: true, email: false));
            var id = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddMinutes(90), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            await PushTestHelper.BackdateBooking(ctx, id);
            return id;
        });

        // Emails don't depend on push notifications being set up
        var mediator = Substitute.For<IMediator>();
        await Run(new AppSettings(), mediator);
        await Run(new AppSettings(), mediator);

        var email = mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEmail>().Should().ContainSingle().Subject;
        email.ClubId.Should().Be(TestDataSeed.ClubId);
        email.Type.Should().Be(ClubEmailType.BookingReminder);
        email.Recipient.Should().Be("user@bookennis.com");

        var variables = email.Variables.Should().BeOfType<BookingReminderEmailVariables>().Subject;
        variables.Booking.Court.Should().Be("Court 1");
        variables.Booking.PlayMode.Should().Be("Single");
        variables.Booking.Url.Should().EndWith($"/clubs/{TestDataSeed.ClubId}/booking/{bookingId}");
    }

    [Fact]
    public async Task SendBookingReminders_PlayerSwitchedThePushReminderOff_GetsNoPushNotification()
    {
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");
            ctx.Add(new NotificationPreference(TestDataSeed.UserId, NotificationType.BookingReminder, push: false, email: true));

            var id = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddMinutes(90), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            await PushTestHelper.BackdateBooking(ctx, id);
        });

        var sent = await Run();

        sent.Should().ContainSingle().Which.UserIds.Should().Equal(TestDataSeed.AdminId);
    }

    [Fact]
    public void Booking_Update_MovedBooking_GetsANewReminder()
    {
        var from = DateTimeOffset.UtcNow.AddHours(1);
        var booking = new Domain.Bookings.Booking(1, 1, 1, new Global.Intervals.DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [1]);
        booking.MarkReminderSent();

        booking.Update(1, 1, booking.Interval, TimeZoneInfo.Utc.Id, [1, 2], "same time");
        booking.ReminderSentAt.Should().NotBeNull();

        booking.Update(1, 1, new Global.Intervals.DateTimeOffsetInterval(from.AddDays(1), from.AddDays(1).AddHours(1)), TimeZoneInfo.Utc.Id, [1, 2], null);
        booking.ReminderSentAt.Should().BeNull();
    }

    private async Task<List<RecordingPushNotificationService.SentNotification>> Run(AppSettings? appSettings = null, IMediator? mediator = null)
    {
        var pushService = new RecordingPushNotificationService();

        await ScopedAsync(() => new SendBookingReminders.Handler(GetInstance<AppDbContext>(), pushService, mediator ?? Substitute.For<IMediator>(), appSettings ?? PushTestHelper.ConfiguredSettings())
            .Handle(new SendBookingReminders(), CancellationToken.None));

        return pushService.Sent;
    }
}
