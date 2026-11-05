using Bookennis.Api.Business.PlayRequests;
using Bookennis.Api.Tests.Business.Push;
using Bookennis.Domain.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.PlayRequests;

public class PlayRequestPushNotifierTests(TestFixture fixture) : TestBase(fixture)
{
    private async Task<List<RecordingPushNotificationService.SentNotification>> Notify(Func<PlayRequestPushNotifier, Task> act)
    {
        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => act(new PlayRequestPushNotifier(GetInstance<Data.AppDbContext>(), pushService)));
        return pushService.Sent;
    }

    [Fact]
    public async Task RequestCreated_NotifiesTheInviteesButNotTheRequester()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, requesterUser, "https://fcm.googleapis.com/fcm/send/requester");
            await PushTestHelper.AddSubscription(ctx, bobUser, "https://fcm.googleapis.com/fcm/send/bob");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");
        });
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [bob, TestDataSeed.Member2Id]));

        var sent = await Notify(n => n.RequestCreated(request.Id, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().BeEquivalentTo([bobUser, TestDataSeed.AdminId]);
        notification.English.Title.Should().Be("alice Player is looking for players");
        notification.English.Body.Should().StartWith("Single · ");
        notification.German.Title.Should().Be("alice Player sucht Mitspieler");
        notification.German.Url.Should().Be($"/clubs/{TestDataSeed.ClubId}/play-requests");
    }

    [Fact]
    public async Task RequestCreated_InviteeSwitchedPlayRequestPushOff_IsNotNotified()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId);
            ctx.Add(new NotificationPreference(TestDataSeed.AdminId, NotificationType.PlayRequest, push: false, email: true));
            await ctx.SaveChangesAsync();
        });
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        var sent = await Notify(n => n.RequestCreated(request.Id, CancellationToken.None));

        sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestCreated_InviteeWithoutDevice_IsNotNotified()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [TestDataSeed.Member2Id]));

        (await Notify(n => n.RequestCreated(request.Id, CancellationToken.None))).Should().BeEmpty();
    }

    [Fact]
    public async Task Accepted_TellsTheRequesterHowManyPlayersAreMissing()
    {
        var (requesterUser, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (_, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        await QueryAsync(ctx => PushTestHelper.AddSubscription(ctx, requesterUser));
        var (_, carol) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [bob, carol, TestDataSeed.Member2Id], playersNeeded: 4));
        await QueryAsync(async ctx =>
        {
            var saved = await ctx.PlayRequests.Include(r => r.Invitees).SingleAsync();
            saved.Accept(bob, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
        });

        var sent = await Notify(n => n.Accepted(request.Id, bob, CancellationToken.None));

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserIds.Should().Equal(requesterUser);
        notification.English.Title.Should().Be("bob Player wants to play");
        notification.English.Body.Should().EndWith("still looking for 2");
        notification.German.Body.Should().EndWith("noch 2 gesucht");
    }

    [Fact]
    public async Task Cancelled_NotifiesInviteesWhoHaveNotDeclined()
    {
        var (_, requester) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var (carolUser, carol) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "carol"));
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, bobUser, "https://fcm.googleapis.com/fcm/send/bob");
            await PushTestHelper.AddSubscription(ctx, carolUser, "https://fcm.googleapis.com/fcm/send/carol");
        });
        var request = await QueryAsync(ctx => PlayRequestSeed.AddRequest(ctx, requester, [bob, carol]));
        await QueryAsync(async ctx =>
        {
            var saved = await ctx.PlayRequests.Include(r => r.Invitees).SingleAsync();
            saved.Decline(bob, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
        });

        var sent = await Notify(n => n.Cancelled(request.Id, CancellationToken.None));

        sent.Should().ContainSingle().Which.UserIds.Should().Equal(carolUser);
    }
}
