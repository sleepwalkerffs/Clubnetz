using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using OrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;

namespace Bookennis.Api.Tests.Business.Memberships;

public class GetMembershipOrderTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMembershipOrder_ReturnsTheOrderWithoutNoteOrStripeIds()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id, MembershipSeed.Data("Passive", 3000) with { GrantsSeasonEnrollment = false });
            var order = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member2Id, TestDataSeed.Member1Id);
            order.SetNote("internal note");
            order.StartCheckout("cs_secret");
            await ctx.SaveChangesAsync();
            return order.Id;
        });

        var result = await SendAsync(new GetMembershipOrder(TestDataSeed.ClubId, orderId));

        result.Should().BeEquivalentTo(new { Id = orderId, PlanName = "Passive", GrantsSeasonEnrollment = false, PayerMemberId = TestDataSeed.Member2Id, PayerName = "ad min", IsFree = false }, o => o.ExcludingMissingMembers());
        result.Members.Should().ContainSingle().Which.Should().BeEquivalentTo(new { MemberId = TestDataSeed.Member1Id, FirstName = "us", LastName = "er" });
        System.Text.Json.JsonSerializer.Serialize(result).Should().NotContain("internal note").And.NotContain("cs_secret");
    }

    [Fact]
    public async Task GetMembershipOrder_OrderOfAnotherClub_IsNotFound()
    {
        var orderId = await QueryAsync(async ctx =>
        {
            var plan = await MembershipSeed.SeedPlan(ctx, (await MembershipSeed.SeedSeason(ctx)).Id);
            return (await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
        });

        var act = () => SendAsync(new GetMembershipOrder(TestDataSeed.ClubId + 1, orderId));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
