using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Security;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Push;

public class BookingPushNotifierTests(TestFixture fixture) : TestBase(fixture)
{
    // 3 October 2037 is a Saturday
    private static readonly DateTimeOffset Start = new(2037, 10, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BookingAdded_NotifiesTheOtherPlayersButNotTheBooker()
    {
        var bookingId = await SeedBookingWithSubscriptions();

        var sent = await Notify(TestDataSeed.UserId, notifier => notifier.BookingAdded(bookingId, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.AdminId);
        notification.English.Title.Should().Be("New booking · Sat 3 October, 18:00");
        notification.English.Body.Should().Be("Court 1 · booked by us er");
        notification.German.Title.Should().Be("Neue Buchung · Sa 3. Oktober, 18:00");
        notification.German.Body.Should().Be("Court 1 · eingetragen von us er");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/booking/{bookingId}");
    }

    [Fact]
    public async Task BookingAdded_PlayerIsChildWithoutLogin_NotifiesTheParent()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            var (_, childMemberId) = await MemberSeed.AddChild(ctx, TestDataSeed.AdminId, "Kid");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId);
            return await MemberSeed.AddBooking(ctx, Start, TestDataSeed.Member1Id, childMemberId);
        });

        var sent = await Notify(TestDataSeed.UserId, notifier => notifier.BookingAdded(bookingId, CancellationToken.None));

        sent.Should().ContainSingle().Which.UserIds.Should().Equal(TestDataSeed.AdminId);
    }

    [Fact]
    public async Task BookingAdded_PlayersWithoutDevices_NotifiesNobody()
    {
        var bookingId = await QueryAsync(ctx => MemberSeed.AddBooking(ctx, Start, TestDataSeed.Member1Id, TestDataSeed.Member2Id));

        var sent = await Notify(TestDataSeed.UserId, notifier => notifier.BookingAdded(bookingId, CancellationToken.None));

        sent.Should().BeEmpty();
    }

    [Fact]
    public async Task BookingAdded_BookingOfSeriesOrInThePast_NotifiesNobody()
    {
        var (seriesBookingId, pastBookingId) = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId);
            var seriesId = await AddSeries(ctx);
            var playModeId = ctx.TestData().Club.PlayModes[0].Id;

            var seriesBooking = new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, new DateTimeOffsetInterval(Start, Start.AddHours(1)), TimeZoneInfo.Utc.Id, [TestDataSeed.Member1Id, TestDataSeed.Member2Id], recurringBookingSeriesId: seriesId);
            ctx.Add(seriesBooking);
            await ctx.SaveChangesAsync();

            var pastBooking = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddDays(-1), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            return (seriesBooking.Id, pastBooking);
        });

        var sent = await Notify(TestDataSeed.UserId, async notifier =>
        {
            await notifier.BookingAdded(seriesBookingId, CancellationToken.None);
            await notifier.BookingAdded(pastBookingId, CancellationToken.None);
        });

        sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RecurringBookingAdded_NotifiesTheOtherPlayersOnce()
    {
        var seriesId = await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId);
            return await AddSeries(ctx);
        });

        var sent = await Notify(TestDataSeed.UserId, notifier => notifier.RecurringBookingAdded(seriesId, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.AdminId);
        notification.English.Title.Should().Be("New recurring booking · Saturday, 18:00");
        notification.German.Title.Should().Be("Neue Serienbuchung · Samstag, 18:00");
        notification.German.Body.Should().Be("Court 1 · eingetragen von us er");
    }

    [Fact]
    public async Task BookingDeleted_NotifiesTheOtherPlayers()
    {
        var bookingId = await SeedBookingWithSubscriptions();

        var sent = await Notify(TestDataSeed.AdminId, notifier => notifier.BookingDeleted(bookingId, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.UserId);
        notification.English.Title.Should().Be("Booking cancelled · Sat 3 October, 18:00");
        notification.English.Body.Should().Be("Court 1 · cancelled by ad min");
        notification.German.Title.Should().Be("Buchung storniert · Sa 3. Oktober, 18:00");
    }

    [Fact]
    public async Task BookingDeleted_WithoutSignedInUser_NotifiesAllPlayersWithoutNamingAnyone()
    {
        var bookingId = await SeedBookingWithSubscriptions();

        var sent = await Notify(actorUserId: null, notifier => notifier.BookingDeleted(bookingId, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(TestDataSeed.AdminId, TestDataSeed.UserId);
        notification.English.Body.Should().Be("Court 1");
    }

    [Fact]
    public async Task RecurringBookingDeleted_NamesTheFirstCancelledDate()
    {
        var bookingId = await SeedBookingWithSubscriptions();

        var sent = await Notify(TestDataSeed.AdminId, notifier => notifier.RecurringBookingDeleted(bookingId, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.English.Title.Should().Be("Recurring booking cancelled from Sat 3 October");
        notification.German.Title.Should().Be("Serienbuchung ab Sa 3. Oktober storniert");
    }

    private Task<int> SeedBookingWithSubscriptions()
        => QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");
            return await MemberSeed.AddBooking(ctx, Start, TestDataSeed.Member1Id, TestDataSeed.Member2Id);
        });

    private static async Task<int> AddSeries(AppDbContext ctx)
    {
        var playModeId = ctx.TestData().Club.PlayModes[0].Id;
        var series = new RecurringBookingSeries(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, DayOfWeek.Saturday, new TimeOnly(18, 0), new TimeOnly(19, 0),
            TimeZoneInfo.Utc.Id, 1, DateOnly.FromDateTime(Start.Date), null, null, [TestDataSeed.Member1Id, TestDataSeed.Member2Id]);
        ctx.Add(series);
        await ctx.SaveChangesAsync();
        return series.Id;
    }

    private async Task<List<RecordingPushNotificationService.SentNotification>> Notify(int? actorUserId, Func<BookingPushNotifier, Task> act)
    {
        var pushService = new RecordingPushNotificationService();
        var userAccessor = actorUserId is { } userId ? IdentityTestHelper.CreateUserAccessor(userId) : Substitute.For<IUserAccessor>();

        await ScopedAsync(() => act(new BookingPushNotifier(GetInstance<AppDbContext>(), pushService, userAccessor)));

        return pushService.Sent;
    }
}
