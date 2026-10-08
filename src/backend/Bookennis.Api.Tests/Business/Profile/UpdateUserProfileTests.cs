using System.Security.Claims;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class UpdateUserProfileTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateUserProfile_UpdatesAllFields()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(userId);
            await handler.Handle(new UpdateUserProfile("UpdatedFirst", "UpdatedLast", new DateOnly(1995, 6, 15), Gender.Female, Language.English, "Main St 1", "Vienna", "1010", Country.Austria), CancellationToken.None);
        });

        var user = await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId));
        user.FirstName.Should().Be("UpdatedFirst");
        user.LastName.Should().Be("UpdatedLast");
        user.Birthday.Should().Be(new DateOnly(1995, 6, 15));
        user.Gender.Should().Be(Gender.Female);
        user.Language.Should().Be(Language.English);
        user.Street.Should().Be("Main St 1");
        user.City.Should().Be("Vienna");
        user.ZipCode.Should().Be("1010");
        user.Country.Should().Be(Country.Austria);
    }

    [Fact]
    public async Task UpdateUserProfile_UpdatesLanguageToGerman()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(userId);
            await handler.Handle(new UpdateUserProfile("First", "Last", new DateOnly(2000, 1, 1), Gender.Male, Language.German, "Street", "City", "12345", Country.Germany), CancellationToken.None);
        });

        var user = await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId));
        user.Language.Should().Be(Language.German);
    }

    [Fact]
    public async Task UpdateUserProfile_WithoutAddress_ClearsAddress()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(userId);
            await handler.Handle(new UpdateUserProfile("First", "Last", new DateOnly(2000, 1, 1), Gender.Male, Language.German, null, null, null, Country.Austria), CancellationToken.None);
        });

        var user = await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId));
        user.Street.Should().BeEmpty();
        user.City.Should().BeEmpty();
        user.ZipCode.Should().BeEmpty();
    }

    private UpdateUserProfile.Handler CreateHandler(int userId)
    {
        var userAccessor = Substitute.For<IUserAccessor>();
        userAccessor.TryGetUser(out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });

        var signInManager = Substitute.For<SignInManager<User>>(
            Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            null, null, null, null);

        return new UpdateUserProfile.Handler(GetInstance<AppDbContext>(), userAccessor, signInManager);
    }
}
