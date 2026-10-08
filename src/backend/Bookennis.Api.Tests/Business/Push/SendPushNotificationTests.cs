using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Push;

public class SendPushNotificationTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly PushNotification Notification = new("Title", "Body", "/account/profile");

    [Fact]
    public async Task SendPushNotification_SendsToAllDevicesOfTheUsers()
    {
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/phone");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://web.push.apple.com/tablet");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/other");
        });

        var sender = Substitute.For<IPushSender>();
        sender.Send(default!, default!, default).ReturnsForAnyArgs(PushSendResult.Sent);

        await Send(sender, TestDataSeed.UserId);

        await sender.Received(2).Send(Arg.Is<PushSubscription>(s => s.UserId == TestDataSeed.UserId), Notification, Arg.Any<CancellationToken>());
        await sender.DidNotReceive().Send(Arg.Is<PushSubscription>(s => s.UserId == TestDataSeed.AdminId), Arg.Any<PushNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPushNotification_SubscriptionGone_RemovesItAndKeepsTheOthers()
    {
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/gone");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/failing");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/ok");
        });

        var sender = Substitute.For<IPushSender>();
        sender.Send(default!, default!, default).ReturnsForAnyArgs(call => call.Arg<PushSubscription>().Endpoint switch
        {
            var e when e.EndsWith("/gone") => PushSendResult.SubscriptionGone,
            var e when e.EndsWith("/failing") => PushSendResult.Failed,
            _ => PushSendResult.Sent
        });

        await Send(sender, TestDataSeed.UserId);

        var endpoints = await QueryAsync(ctx => ctx.PushSubscriptions.Select(s => s.Endpoint).ToListAsync());
        endpoints.Should().BeEquivalentTo(["https://fcm.googleapis.com/fcm/send/failing", "https://fcm.googleapis.com/fcm/send/ok"]);
    }

    [Fact]
    public async Task PushNotificationService_SendsOneCommandPerLanguage()
    {
        var mediator = Substitute.For<IMediator>();
        var service = new PushNotificationService(mediator, PushTestHelper.ConfiguredSettings());

        await service.Notify(
            [new PushRecipient(1, Language.German), new PushRecipient(2, Language.German), new PushRecipient(3, Language.English)],
            culture => new PushNotification(culture.TwoLetterISOLanguageName, "Body", "/"),
            CancellationToken.None);

        await mediator.Received(1).Send(Arg.Is<SendPushNotification>(c => c.Notification.Title == "de" && c.UserIds.SequenceEqual(new[] { 1, 2 })), Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(Arg.Is<SendPushNotification>(c => c.Notification.Title == "en" && c.UserIds.SequenceEqual(new[] { 3 })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushNotificationService_PushNotConfigured_SendsNothing()
    {
        var mediator = Substitute.For<IMediator>();
        var service = new PushNotificationService(mediator, new AppSettings());

        await service.Notify([new PushRecipient(1, Language.German)], _ => Notification, CancellationToken.None);

        await mediator.DidNotReceiveWithAnyArgs().Send(default(SendPushNotification)!, default);
    }

    [Fact]
    public async Task SendTestPushNotification_NotifiesTheCurrentUser()
    {
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/mine");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/other");
        });

        var pushService = new RecordingPushNotificationService();
        await ScopedAsync(() => new SendTestPushNotification.Handler(GetInstance<AppDbContext>(), IdentityTestHelper.CreateUserAccessor(TestDataSeed.UserId), pushService)
            .Handle(new SendTestPushNotification(), CancellationToken.None));

        var sent = pushService.Sent.Should().ContainSingle().Subject;
        sent.UserIds.Should().Equal(TestDataSeed.UserId);
        sent.English.Title.Should().Be("It works 🎾");
        sent.German.Title.Should().Be("Es funktioniert 🎾");
        sent.German.Url.Should().Be("/account/profile");
    }

    private Task Send(IPushSender sender, params int[] userIds)
        => ScopedAsync(() => new SendPushNotification.Handler(GetInstance<AppDbContext>(), sender)
            .Handle(new SendPushNotification(userIds, Notification), CancellationToken.None));
}
