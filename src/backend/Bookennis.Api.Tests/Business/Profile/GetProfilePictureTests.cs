using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

/// <summary>Profile pictures are only visible to the user, application administrators and people who share a club with the user.</summary>
public class GetProfilePictureTests(TestFixture fixture) : TestBase(fixture)
{
    private const int StrangerId = 1000_0099;

    [Fact]
    public async Task MemberOfTheSameClub_GetsThePicture()
    {
        await SeedPictureOfAdmin();

        var result = await SendAsync(new GetProfilePicture(TestDataSeed.AdminId, TestDataSeed.UserId, false));

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task OwnPicture_IsReturned()
    {
        await SeedPictureOfAdmin();

        var result = await SendAsync(new GetProfilePicture(TestDataSeed.AdminId, TestDataSeed.AdminId, false));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UserWithoutASharedClub_GetsNothing()
    {
        await SeedPictureOfAdmin();
        await AddStranger();

        var result = await SendAsync(new GetProfilePicture(TestDataSeed.AdminId, StrangerId, false));

        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplicationAdministratorWithoutASharedClub_GetsThePicture()
    {
        await SeedPictureOfAdmin();
        await AddStranger();

        var result = await SendAsync(new GetProfilePicture(TestDataSeed.AdminId, StrangerId, true));

        result.Should().NotBeNull();
    }

    private Task SeedPictureOfAdmin() => QueryAsync(async ctx =>
    {
        ctx.UserProfilePictures.Add(new UserProfilePicture(TestDataSeed.AdminId, [1, 2, 3], "image/jpeg"));
        await ctx.SaveChangesAsync();
    });

    private Task AddStranger() => QueryAsync(async ctx =>
    {
        var stranger = new User("stranger@bookennis.com", "stranger@bookennis.com", "str", "anger", new DateOnly(2000, 1, 1), Gender.Male, "Street 1", "Linz", "4020", Country.Austria);
        stranger.SetId(StrangerId);
        ctx.Add(stranger);
        await ctx.SaveChangesAsync();
    });
}
