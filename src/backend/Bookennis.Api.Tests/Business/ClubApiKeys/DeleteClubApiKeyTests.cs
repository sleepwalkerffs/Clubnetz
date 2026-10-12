using Bookennis.Api.Business.ClubApiKeys;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubApiKeys;

public class DeleteClubApiKeyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteClubApiKey_RemovesTheKeySoItIsRejected()
    {
        var (id, token) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        await SendAsync(new DeleteClubApiKey(TestDataSeed.ClubId, id));

        (await QueryAsync(ctx => ctx.ClubApiKeys.CountAsync())).Should().Be(0);
        (await SendAsync(new AuthenticateClubApiKey(token, TestDataSeed.ClubId))).Should().BeNull();
    }

    [Fact]
    public async Task DeleteClubApiKey_KeyOfAnotherClub_ThrowsEntityNotFoundException()
    {
        var (id, _) = await QueryAsync(ctx => ClubApiKeySeed.Seed(ctx));

        var act = () => SendAsync(new DeleteClubApiKey(TestDataSeed.ClubId + 1, id));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
