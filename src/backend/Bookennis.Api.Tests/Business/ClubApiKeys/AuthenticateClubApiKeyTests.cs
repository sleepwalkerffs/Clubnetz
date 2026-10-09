using Bookennis.Api.Business.ClubApiKeys;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubApiKeys;

public class AuthenticateClubApiKeyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AuthenticateClubApiKey_ValidKey_ReturnsItsCreatorAndRemembersTheUse()
    {
        var (id, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx, isReadOnly: true));

        var result = await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId));

        result.Should().Be(new AuthenticatedClubApiKey(id, TestDataSeed.ClubId, TestDataSeed.AdminId, IsReadOnly: true));
        var key = await QueryAsync(ctx => ctx.ClubApiKeys.SingleAsync());
        key.LastUsedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AuthenticateClubApiKey_UsedAgainShortlyAfter_DoesNotWriteTheLastUseAgain()
    {
        var (_, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId));
        var firstUse = await QueryAsync(ctx => ctx.ClubApiKeys.Select(k => k.LastUsedAt).SingleAsync());

        var result = await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId));

        result.Should().NotBeNull();
        (await QueryAsync(ctx => ctx.ClubApiKeys.Select(k => k.LastUsedAt).SingleAsync())).Should().Be(firstUse);
    }

    [Fact]
    public async Task AuthenticateClubApiKey_WorksBeforeTheTenantIsKnown()
    {
        var (id, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        // The tenant filter must not hide the key or the admins of its club
        SetTenantId(TestDataSeed.ClubId + 1);
        var result = await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId));

        result!.ClubApiKeyId.Should().Be(id);
    }

    [Fact]
    public async Task AuthenticateClubApiKey_ForAnotherClub_ReturnsNull()
    {
        var (_, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        var result = await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId + 1));

        result.Should().BeNull();
        (await QueryAsync(ctx => ctx.ClubApiKeys.Select(k => k.LastUsedAt).SingleAsync())).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-key")]
    [InlineData(ClubApiKey.TokenPrefix)]
    [InlineData(ClubApiKey.TokenPrefix + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AuthenticateClubApiKey_UnknownKey_ReturnsNull(string token)
    {
        await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        var result = await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId));

        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateClubApiKey_KeyHash_IsNotAccepted()
    {
        var (_, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        // Somebody who can read the database must not be able to use what is stored there
        var result = await SendAsync(new AuthenticateClubApiKey(ClubApiKey.TokenPrefix + ClubApiKey.Hash(token), TestDataSeed.ClubId));

        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateClubApiKey_ExpiredKey_ReturnsNull()
    {
        var (_, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var result = await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId));

        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateClubApiKey_CreatorIsNoLongerAdmin_ReturnsNull()
    {
        var (token, memberId) = await QueryAsync(async ctx =>
        {
            var (userId, adminMemberId) = await MemberSeed.AddUserWithMember(ctx, "former.admin@bookennis.com", MemberRole.Admin);
            var key = await ClubApiKeySeed.Seed(ctx, userId: userId);
            return (key.Token, adminMemberId);
        });

        (await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId))).Should().NotBeNull();

        await QueryAsync(async ctx =>
        {
            var member = await ctx.ClubMembers.FindAsync(memberId);
            member!.UpdateRoles([MemberRole.User, MemberRole.Maintainer]);
            await ctx.SaveChangesAsync();
        });

        (await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId))).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateClubApiKey_CreatorLeftTheClub_ReturnsNull()
    {
        var (token, memberId) = await QueryAsync(async ctx =>
        {
            var (userId, adminMemberId) = await MemberSeed.AddUserWithMember(ctx, "former.admin@bookennis.com", MemberRole.Admin);
            var key = await ClubApiKeySeed.Seed(ctx, userId: userId);
            return (key.Token, adminMemberId);
        });

        await QueryAsync(async ctx =>
        {
            ctx.ClubMembers.Remove((await ctx.ClubMembers.FindAsync(memberId))!);
            await ctx.SaveChangesAsync();
        });

        (await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId))).Should().BeNull();
    }
}
