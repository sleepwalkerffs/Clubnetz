using Bookennis.Api.Business.ClubApiKeys;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubApiKeys;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubApiKeys;

public class GetClubApiKeysTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubApiKeys_ReturnsKeysNewestFirstWithoutTheKeyItself()
    {
        var (firstToken, secondId) = await QueryAsync(async ctx =>
        {
            var first = await ClubApiKeySeed.Seed(ctx, "Website");
            var second = await ClubApiKeySeed.Seed(ctx, "Export", isReadOnly: true, expiresAt: DateTimeOffset.UtcNow.AddDays(10));
            return (first.Token, second.Id);
        });

        var result = await SendAsync(new GetClubApiKeys(TestDataSeed.ClubId));

        result.ApiKeys.Select(k => k.Name).Should().Equal("Export", "Website");

        var export = result.ApiKeys[0];
        export.Id.Should().Be(secondId);
        export.IsReadOnly.Should().BeTrue();
        export.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(10), TimeSpan.FromMinutes(1));
        export.LastUsedAt.Should().BeNull();
        export.CreatedByName.Should().Be("ad min");
        export.Status.Should().Be(ClubApiKeyStatus.Active);

        var website = result.ApiKeys[1];
        website.IsReadOnly.Should().BeFalse();
        website.ExpiresAt.Should().BeNull();
        firstToken.Should().StartWith(website.KeyPrefix);
        website.KeyPrefix.Should().NotBe(firstToken);
    }

    [Fact]
    public async Task GetClubApiKeys_ReportsExpiredKeysAndKeysOfFormerAdmins()
    {
        await QueryAsync(async ctx =>
        {
            await ClubApiKeySeed.Seed(ctx, "Expired", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

            var (formerAdminUserId, formerAdminMemberId) = await MemberSeed.AddUserWithMember(ctx, "former.admin@bookennis.com", MemberRole.Admin);
            await ClubApiKeySeed.Seed(ctx, "Former admin", userId: formerAdminUserId);

            var member = await ctx.ClubMembers.FindAsync(formerAdminMemberId);
            member!.UpdateRoles([MemberRole.User]);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubApiKeys(TestDataSeed.ClubId));

        result.ApiKeys.Single(k => k.Name == "Expired").Status.Should().Be(ClubApiKeyStatus.Expired);
        result.ApiKeys.Single(k => k.Name == "Former admin").Status.Should().Be(ClubApiKeyStatus.CreatorNotAdmin);
    }

    [Fact]
    public async Task GetClubApiKeys_OfAnotherClub_ReturnsNothing()
    {
        await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        var result = await SendAsync(new GetClubApiKeys(TestDataSeed.ClubId + 1));

        result.ApiKeys.Should().BeEmpty();
    }
}
