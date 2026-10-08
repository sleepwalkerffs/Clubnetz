using System.Security.Claims;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace Bookennis.Api.Tests.TestUtils;

/// <summary>ASP.NET Identity is not registered in the test container, so identity based handlers get substitutes.</summary>
public static class IdentityTestHelper
{
    public static UserManager<User> CreateUserManager()
        => Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);

    public static SignInManager<User> CreateSignInManager(UserManager<User>? userManager = null)
        => Substitute.For<SignInManager<User>>(
            userManager ?? CreateUserManager(),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            null, null, null, null);

    public static IUserAccessor CreateUserAccessor(int userId)
    {
        var userAccessor = Substitute.For<IUserAccessor>();
        userAccessor.TryGetUser(out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });
        return userAccessor;
    }
}
