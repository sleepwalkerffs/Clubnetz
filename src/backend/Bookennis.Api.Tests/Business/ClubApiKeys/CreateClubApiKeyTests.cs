using Bookennis.Api.Business.ClubApiKeys;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubApiKeys;

public class CreateClubApiKeyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateClubApiKey_ReturnsTokenAndStoresOnlyItsHash()
    {
        var result = await SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.AdminId, "  Website  ", IsReadOnly: true, ExpiresInDays: null));

        result.Token.Should().StartWith(ClubApiKey.TokenPrefix);
        result.Token.Length.Should().BeGreaterThan(40);

        var key = await QueryAsync(ctx => ctx.ClubApiKeys.SingleAsync());
        key.Id.Should().Be(result.Id);
        key.ClubId.Should().Be(TestDataSeed.ClubId);
        key.CreatedByUserId.Should().Be(TestDataSeed.AdminId);
        key.Name.Should().Be("Website");
        key.IsReadOnly.Should().BeTrue();
        key.ExpiresAt.Should().BeNull();
        key.LastUsedAt.Should().BeNull();
        key.KeyHash.Should().Be(ClubApiKey.Hash(result.Token));
        key.KeyHash.Should().NotContain(result.Token);
        result.Token.Should().StartWith(key.KeyPrefix);
        key.KeyPrefix.Length.Should().BeLessThan(result.Token.Length / 2);
    }

    [Fact]
    public async Task CreateClubApiKey_WithExpiry_SetsExpiresAt()
    {
        await SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.AdminId, "Website", IsReadOnly: false, ExpiresInDays: 30));

        var key = await QueryAsync(ctx => ctx.ClubApiKeys.SingleAsync());
        key.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateClubApiKey_WithoutName_ThrowsPreconditionException(string? name)
    {
        var act = () => SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.AdminId, name, IsReadOnly: false, ExpiresInDays: null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateClubApiKey.ErrorCode.ApiKeyNameRequired));
    }

    [Fact]
    public async Task CreateClubApiKey_NameTooLong_ThrowsPreconditionException()
    {
        var name = new string('a', ClubApiKey.NameMaxLength + 1);

        var act = () => SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.AdminId, name, IsReadOnly: false, ExpiresInDays: null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateClubApiKey.ErrorCode.ApiKeyNameTooLong));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ClubApiKey.MaxExpiryInDays + 1)]
    public async Task CreateClubApiKey_InvalidExpiry_ThrowsPreconditionException(int expiresInDays)
    {
        var act = () => SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.AdminId, "Website", IsReadOnly: false, expiresInDays));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateClubApiKey.ErrorCode.ApiKeyInvalidExpiry));
    }

    [Fact]
    public async Task CreateClubApiKey_UserIsNoAdminOfTheClub_ThrowsPreconditionException()
    {
        // Member of the club, but not an admin
        var act = () => SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.UserId, "Website", IsReadOnly: false, ExpiresInDays: null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateClubApiKey.ErrorCode.ApiKeyCreatorNotClubAdmin));
        (await QueryAsync(ctx => ctx.ClubApiKeys.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CreateClubApiKey_LimitReached_ThrowsPreconditionException()
    {
        await QueryAsync(async ctx =>
        {
            for (var i = 0; i < ClubApiKey.MaxKeysPerClub; i++)
                await ClubApiKeySeed.Seed(ctx, $"Key {i}");
        });

        var act = () => SendAsync(new CreateClubApiKey(TestDataSeed.ClubId, TestDataSeed.AdminId, "One too many", IsReadOnly: false, ExpiresInDays: null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreateClubApiKey.ErrorCode.ApiKeyLimitReached));
    }
}
