using System.Security.Claims;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.Business.ClubEvents;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Common.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class ExportPersonalDataTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Export_ContainsAccountMembershipsBookingsEventsBadgesPlansAndChildren()
    {
        var bookingFrom = DateTimeOffset.UtcNow.AddDays(-5);
        await QueryAsync(async ctx =>
        {
            var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            ctx.Add(new MemberSeason(TestDataSeed.Member1Id, season.Id));

            var tier = new BadgeTier(TestDataSeed.ClubId, season.Id, 1, "Bronze", "First steps", 1, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();
            ctx.Add(new MemberBadge(TestDataSeed.Member1Id, tier.Id, season.Id));

            var plan = new SubscriptionPlan(TestDataSeed.ClubId, TestDataSeed.UserId, "Winter", new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 31), 2);
            plan.Update("Winter", plan.StartDate, plan.EndDate, 2, [], [new(null, "Anna", 50, 0, []), new(null, "Ben", 50, 1, [])]);
            ctx.Add(plan);
            await ctx.SaveChangesAsync();

            await MemberSeed.AddBooking(ctx, bookingFrom, TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            await MemberSeed.AddChild(ctx, TestDataSeed.UserId, "Kid");
            await ClubEventSeed.SeedEventWithRegistrations(ctx);
        });
        SetCurrentUser(TestDataSeed.UserId);

        var export = await SendAsync(new ExportPersonalData());

        export.Account.Email.Should().Be("user@bookennis.com");
        export.Account.City.Should().Be("Graz");

        var membership = export.Memberships.Should().ContainSingle().Which;
        membership.Club.Should().Be("TestClub");
        membership.MemberType.Should().Be(MemberType.ClubMember);
        membership.Seasons.Should().ContainSingle().Which.From.Should().Be(new DateOnly(2026, 1, 1));

        var booking = export.Bookings.Should().ContainSingle().Which;
        booking.Court.Should().Be("Court 1");
        booking.CoPlayers.Should().BeEquivalentTo("ad min");

        var registration = export.EventRegistrations.Should().ContainSingle().Which;
        registration.HeadCount.Should().Be(3);
        registration.Comment.Should().Be("Bringing a cake");
        registration.Answers.Select(a => (a.Option, a.Quantity)).Should().BeEquivalentTo([("Yes", 1), ("Schnitzel", 2), ("Veggie", 1)]);

        export.Badges.Should().ContainSingle().Which.Name.Should().Be("Bronze");
        export.SubscriptionPlans.Should().ContainSingle().Which.Participants.Select(p => p.Name).Should().Equal("Anna", "Ben");
        export.Children.Should().ContainSingle().Which.FirstName.Should().Be("Kid");
    }

    [Fact]
    public async Task Export_DoesNotContainDataOfOtherUsers()
    {
        await QueryAsync(ctx => MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddDays(-1), TestDataSeed.Member2Id));
        SetCurrentUser(TestDataSeed.UserId);

        var export = await SendAsync(new ExportPersonalData());

        export.Bookings.Should().BeEmpty();
        export.Memberships.Should().ContainSingle();
    }

    private void SetCurrentUser(int userId)
    {
        var userAccessor = GetInstance<IUserAccessor>();
        userAccessor.TryGetUser(user: out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });
    }
}
