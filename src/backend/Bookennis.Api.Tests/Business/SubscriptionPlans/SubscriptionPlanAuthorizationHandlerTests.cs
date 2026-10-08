using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bookennis.Api.Infrastructure.Authorization.Handler;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class SubscriptionPlanAuthorizationHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Owner_Succeeds()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId)).Id);

        var succeeded = await Authorize(TestDataSeed.UserId, planId);

        succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task OtherUser_Fails()
    {
        var planId = await QueryAsync(async ctx => (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId)).Id);

        var succeeded = await Authorize(TestDataSeed.AdminId, planId);

        succeeded.Should().BeFalse();
    }

    private Task<bool> Authorize(int userId, int planId)
        => QueryAsync(async ctx =>
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(System.Globalization.CultureInfo.InvariantCulture))], "Test"));
            var requirement = new OwnsSubscriptionPlanRequirement();
            var context = new AuthorizationHandlerContext([requirement], user, new SubscriptionPlanAuthorizationModel(planId));

            await new SubscriptionPlanAuthorizationHandler(ctx).HandleAsync(context);

            return context.HasSucceeded;
        });
}
