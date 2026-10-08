using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Push;

public class PushSubscriptionTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetPushConfiguration_ReturnsPublicKeyOnlyWhenConfigured()
    {
        var configured = await new GetPushConfiguration.Handler(PushTestHelper.ConfiguredSettings()).Handle(new GetPushConfiguration(), CancellationToken.None);
        var notConfigured = await new GetPushConfiguration.Handler(new AppSettings()).Handle(new GetPushConfiguration(), CancellationToken.None);

        configured.PublicKey.Should().Be("public");
        notConfigured.PublicKey.Should().BeNull();
    }

    [Fact]
    public async Task SavePushSubscription_NewDevice_StoresSubscription()
    {
        await Save(TestDataSeed.UserId, new SavePushSubscription(PushTestHelper.Endpoint, "key", "auth"));

        var subscription = await QueryAsync(ctx => ctx.PushSubscriptions.SingleAsync());
        subscription.UserId.Should().Be(TestDataSeed.UserId);
        subscription.Endpoint.Should().Be(PushTestHelper.Endpoint);
        subscription.P256dh.Should().Be("key");
        subscription.Auth.Should().Be("auth");
    }

    [Fact]
    public async Task SavePushSubscription_SameDeviceAgain_KeepsOneSubscription()
    {
        await Save(TestDataSeed.UserId, new SavePushSubscription(PushTestHelper.Endpoint, "key", "auth"));
        await Save(TestDataSeed.UserId, new SavePushSubscription(PushTestHelper.Endpoint, "new-key", "new-auth"));

        var subscription = await QueryAsync(ctx => ctx.PushSubscriptions.SingleAsync());
        subscription.P256dh.Should().Be("new-key");
        subscription.Auth.Should().Be("new-auth");
    }

    [Fact]
    public async Task SavePushSubscription_OtherUserOnSameDevice_MovesSubscription()
    {
        await Save(TestDataSeed.UserId, new SavePushSubscription(PushTestHelper.Endpoint, "key", "auth"));
        await Save(TestDataSeed.AdminId, new SavePushSubscription(PushTestHelper.Endpoint, "key", "auth"));

        var subscription = await QueryAsync(ctx => ctx.PushSubscriptions.SingleAsync());
        subscription.UserId.Should().Be(TestDataSeed.AdminId);
    }

    [Fact]
    public async Task SavePushSubscription_MoreThanMaxDevices_RemovesOldest()
    {
        for (var i = 0; i <= SavePushSubscription.MaxDevicesPerUser; i++)
            await Save(TestDataSeed.UserId, new SavePushSubscription($"https://fcm.googleapis.com/fcm/send/device-{i}", "key", "auth"));

        var endpoints = await QueryAsync(ctx => ctx.PushSubscriptions.Select(s => s.Endpoint).ToListAsync());
        endpoints.Should().HaveCount(SavePushSubscription.MaxDevicesPerUser);
        endpoints.Should().NotContain("https://fcm.googleapis.com/fcm/send/device-0");
    }

    [Theory]
    [InlineData("https://evil.example.com/push", "key", "auth")]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", "key", "auth")]
    [InlineData("https://fcm.googleapis.com.evil.example/abc", "key", "auth")]
    [InlineData(null, "key", "auth")]
    [InlineData(PushTestHelper.Endpoint, "", "auth")]
    [InlineData(PushTestHelper.Endpoint, "key", null)]
    public async Task SavePushSubscription_InvalidSubscription_ThrowsPreconditionException(string? endpoint, string? p256dh, string? auth)
    {
        var act = () => Save(TestDataSeed.UserId, new SavePushSubscription(endpoint, p256dh, auth));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(SavePushSubscription.ErrorCode.InvalidPushSubscription));
    }

    [Fact]
    public async Task SavePushSubscription_PushNotConfigured_ThrowsPreconditionException()
    {
        var act = () => Save(TestDataSeed.UserId, new SavePushSubscription(PushTestHelper.Endpoint, "key", "auth"), new AppSettings());

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(SavePushSubscription.ErrorCode.PushNotificationsNotAvailable));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-am3p.notify.windows.com/w/?token=abc", true)]
    [InlineData("https://fcm.googleapis.com:8443/abc", false)]
    [InlineData("https://localhost/abc", false)]
    [InlineData("not a url", false)]
    public void PushEndpoint_IsAllowed_OnlyAcceptsKnownPushServices(string endpoint, bool expected)
        => PushEndpoint.IsAllowed(endpoint).Should().Be(expected);

    [Fact]
    public async Task DeletePushSubscription_OwnDevice_RemovesSubscription()
    {
        await QueryAsync(ctx => PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId));

        await Delete(TestDataSeed.UserId, PushTestHelper.Endpoint);

        (await QueryAsync(ctx => ctx.PushSubscriptions.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task DeletePushSubscription_DeviceOfOtherUser_KeepsSubscription()
    {
        await QueryAsync(ctx => PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId));

        await Delete(TestDataSeed.UserId, PushTestHelper.Endpoint);

        (await QueryAsync(ctx => ctx.PushSubscriptions.AnyAsync())).Should().BeTrue();
    }

    private Task Save(int userId, SavePushSubscription command, AppSettings? appSettings = null)
        => ScopedAsync(() => new SavePushSubscription.Handler(GetInstance<AppDbContext>(), IdentityTestHelper.CreateUserAccessor(userId), appSettings ?? PushTestHelper.ConfiguredSettings())
            .Handle(command, CancellationToken.None));

    private Task Delete(int userId, string endpoint)
        => ScopedAsync(() => new DeletePushSubscription.Handler(GetInstance<AppDbContext>(), IdentityTestHelper.CreateUserAccessor(userId))
            .Handle(new DeletePushSubscription(endpoint), CancellationToken.None));
}
