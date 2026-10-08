using Bookennis.Api.Business.Profile;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class DismissProfileCompletionBannerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Dismiss_PersistsFlag_AndProfileReturnsIt()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        (await SendAsync(new GetUserProfile(userId))).ProfileCompletionBannerDismissed.Should().BeFalse();

        await ScopedAsync(async () =>
        {
            var handler = new DismissProfileCompletionBanner.Handler(GetInstance<AppDbContext>(), IdentityTestHelper.CreateUserAccessor(userId));
            await handler.Handle(new DismissProfileCompletionBanner(), CancellationToken.None);
        });

        var user = await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId));
        user.ProfileCompletionBannerDismissedAt.Should().NotBeNull();
        (await SendAsync(new GetUserProfile(userId))).ProfileCompletionBannerDismissed.Should().BeTrue();
    }

    [Fact]
    public async Task Dismiss_Twice_KeepsFirstTimestamp()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await ScopedAsync(async () =>
            await new DismissProfileCompletionBanner.Handler(GetInstance<AppDbContext>(), IdentityTestHelper.CreateUserAccessor(userId))
                .Handle(new DismissProfileCompletionBanner(), CancellationToken.None));
        var first = (await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId))).ProfileCompletionBannerDismissedAt;

        await ScopedAsync(async () =>
            await new DismissProfileCompletionBanner.Handler(GetInstance<AppDbContext>(), IdentityTestHelper.CreateUserAccessor(userId))
                .Handle(new DismissProfileCompletionBanner(), CancellationToken.None));
        var second = (await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId))).ProfileCompletionBannerDismissedAt;

        second.Should().Be(first);
    }
}
