using System.Security.Claims;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Tests.Business.Memberships;
using Bookennis.Domain.Memberships;
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

    [Fact]
    public async Task Export_ContainsTheMembershipOrdersTheUserPaidOrIsCoveredBy_WithoutNoteOrPaymentProviderIds()
    {
        await QueryAsync(async ctx =>
        {
            var season = await MembershipSeed.SeedSeason(ctx);
            var plan = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.FamilyData());

            var own = await MembershipSeed.SeedOrder(ctx, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            own.SetNote("Note of the treasurer");
            own.StartCheckout("cs_secret");
            own.MarkPaid(MembershipPaymentMethod.Online, new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), paymentIntentId: "pi_secret");

            // Paid by somebody else for the user, and an order the user has nothing to do with
            var stranger = await MembershipSeed.AddMember(ctx, "Stranger");
            var otherSeason = await MembershipSeed.SeedSeason(ctx, new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31));
            var oldPlan = await MembershipSeed.SeedPlan(ctx, otherSeason.Id, MembershipSeed.Data("Old plan", 9000));
            await MembershipSeed.SeedOrder(ctx, oldPlan, stranger, TestDataSeed.Member1Id);
            await MembershipSeed.SeedOrder(ctx, plan, stranger, stranger);
            await ctx.SaveChangesAsync();
        });
        SetCurrentUser(TestDataSeed.UserId);

        var export = await SendAsync(new ExportPersonalData());

        export.MembershipOrders.Select(o => o.Plan).Should().BeEquivalentTo(["Family", "Old plan"]);

        var paid = export.MembershipOrders.Single(o => o.Plan == "Family");
        paid.Should().BeEquivalentTo(new { Club = "TestClub", Amount = 215m, Currency = "eur", Status = MembershipOrderStatus.Paid, PaymentMethod = MembershipPaymentMethod.Online, IsPayer = true, IsCovered = true }, o => o.ExcludingMissingMembers());
        paid.PaidAt.Should().Be(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        var covered = export.MembershipOrders.Single(o => o.Plan == "Old plan");
        covered.Should().BeEquivalentTo(new { IsPayer = false, IsCovered = true, SeasonFrom = new DateOnly(2020, 1, 1), Status = MembershipOrderStatus.AwaitingPayment }, o => o.ExcludingMissingMembers());

        var json = System.Text.Json.JsonSerializer.Serialize(export);
        json.Should().NotContain("Note of the treasurer").And.NotContain("cs_secret").And.NotContain("pi_secret").And.NotContain("Stranger");
    }

    [Fact]
    public async Task Export_ContainsTheOwnClubhouseTabs_NotThoseOfOthers()
    {
        await QueryAsync(async ctx =>
        {
            var beer = new Domain.Clubhouse.ClubhouseItem(TestDataSeed.ClubId, new Domain.Clubhouse.ClubhouseItem.ItemData("Beer", Domain.Clubhouse.ClubhouseItemCategory.Drink, 450, true, 0));
            ctx.Add(beer);
            await ctx.SaveChangesAsync();

            var own = new Domain.Clubhouse.ClubhouseTab(TestDataSeed.ClubId, TestDataSeed.Member1Id, null, TestDataSeed.Member2Id, DateTimeOffset.UtcNow);
            own.Add(beer, 2, null, DateTimeOffset.UtcNow);
            own.MarkPaid(Domain.Clubhouse.ClubhousePaymentMethod.Cash, null, DateTimeOffset.UtcNow);
            var other = new Domain.Clubhouse.ClubhouseTab(TestDataSeed.ClubId, TestDataSeed.Member2Id, null, null, DateTimeOffset.UtcNow);
            other.Add(new Domain.Clubhouse.ClubhouseItem(TestDataSeed.ClubId, new Domain.Clubhouse.ClubhouseItem.ItemData("Secret wine", Domain.Clubhouse.ClubhouseItemCategory.Drink, 900, true, 0)), 1, null, DateTimeOffset.UtcNow);
            ctx.AddRange(own, other);
            await ctx.SaveChangesAsync();
        });
        SetCurrentUser(TestDataSeed.UserId);

        var export = await SendAsync(new ExportPersonalData());

        var tab = export.ClubhouseTabs.Should().ContainSingle().Subject;
        tab.Should().BeEquivalentTo(new { Club = "TestClub", Status = Domain.Clubhouse.ClubhouseTabStatus.Paid, PaymentMethod = (Domain.Clubhouse.ClubhousePaymentMethod?)Domain.Clubhouse.ClubhousePaymentMethod.Cash }, o => o.ExcludingMissingMembers());
        tab.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "Beer", Quantity = 2, UnitPrice = 4.5m }, o => o.ExcludingMissingMembers());
        System.Text.Json.JsonSerializer.Serialize(export).Should().NotContain("Secret wine");
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
