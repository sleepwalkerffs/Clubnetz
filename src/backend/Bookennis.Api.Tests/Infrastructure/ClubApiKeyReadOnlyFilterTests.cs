using System.Net;
using System.Security.Claims;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Shared.Controller.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

public class ClubApiKeyReadOnlyFilterTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void ReadOnlyKey_Writing_IsForbidden(string method)
    {
        var context = Authorize(method, ApiKey(isReadOnly: true));

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        result.Value.Should().BeOfType<ErrorCodeResponse>().Which.ErrorCode.Should().Be(ClubApiKeyReadOnlyFilter.ErrorCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public void ReadOnlyKey_Reading_IsAllowed(string method)
        => Authorize(method, ApiKey(isReadOnly: true)).Result.Should().BeNull();

    [Fact]
    public void FullAccessKey_Writing_IsAllowed()
        => Authorize("DELETE", ApiKey(isReadOnly: false)).Result.Should().BeNull();

    [Fact]
    public void SignedInUser_Writing_IsAllowed()
        => Authorize("POST", new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")], "cookie"))).Result.Should().BeNull();

    private static ClaimsPrincipal ApiKey(bool isReadOnly)
        => new(new ClaimsIdentity(
            [
                new Claim(ClubApiKeyClaims.KeyId, "1"),
                new Claim(ClubApiKeyClaims.ReadOnly, isReadOnly ? "true" : "false"),
            ],
            CustomAuthenticationSchemes.ClubApiKey));

    private static AuthorizationFilterContext Authorize(string method, ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.Method = method;

        var context = new AuthorizationFilterContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);
        new ClubApiKeyReadOnlyFilter().OnAuthorization(context);
        return context;
    }
}
