using System.Net;
using System.Security.Claims;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Shared.Controller.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

public class ClubApiKeyReadOnlyFilterTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task ReadOnlyKey_Writing_IsForbidden_AndLogged(string method)
    {
        var writer = Substitute.For<IAuditWriter>();
        var context = await Authorize(method, ApiKey(isReadOnly: true), writer);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        result.Value.Should().BeOfType<ErrorCodeResponse>().Which.ErrorCode.Should().Be(ClubApiKeyReadOnlyFilter.ErrorCode);
        await writer.Received(1).WriteAsync(Arg.Is<AuditEvent>(e => e.Action == AccessDeniedAudit.Action && e.ErrorCode == ClubApiKeyReadOnlyFilter.ErrorCode));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task ReadOnlyKey_Reading_IsAllowed(string method)
        => (await Authorize(method, ApiKey(isReadOnly: true))).Result.Should().BeNull();

    [Fact]
    public async Task FullAccessKey_Writing_IsAllowed()
        => (await Authorize("DELETE", ApiKey(isReadOnly: false))).Result.Should().BeNull();

    [Fact]
    public async Task SignedInUser_Writing_IsAllowed()
        => (await Authorize("POST", new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")], "cookie")))).Result.Should().BeNull();

    private static ClaimsPrincipal ApiKey(bool isReadOnly)
        => new(new ClaimsIdentity(
            [
                new Claim(ClubApiKeyClaims.KeyId, "1"),
                new Claim(ClubApiKeyClaims.ReadOnly, isReadOnly ? "true" : "false"),
            ],
            CustomAuthenticationSchemes.ClubApiKey));

    private static async Task<AuthorizationFilterContext> Authorize(string method, ClaimsPrincipal user, IAuditWriter? writer = null)
    {
        var auditContext = Substitute.For<IAuditContext>();
        auditContext.GetActor().Returns(new AuditActor(null, null, null, null, null, 1, null, null, null, null));
        var services = new ServiceCollection()
                       .AddSingleton(auditContext)
                       .AddSingleton(writer ?? Substitute.For<IAuditWriter>())
                       .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { User = user, RequestServices = services };
        httpContext.Request.Method = method;

        var context = new AuthorizationFilterContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);
        await new ClubApiKeyReadOnlyFilter().OnAuthorizationAsync(context);
        return context;
    }
}
