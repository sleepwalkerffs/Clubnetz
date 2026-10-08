using System.Security.Claims;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Guests;

public class GuestAuthorizationTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMember_GuestSession_WithDualMembership_ReturnsGuestMember()
    {
        var (clubId, guestMemberId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = ctx.TestData().User;

            // User already has a ClubMember (from seed), now also add a GuestMember
            var guestMember = new GuestMember(user.Id, club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            return (club.Id, guestMember.Id);
        });

        var result = await ScopedAsync(() =>
        {
            var service = GetInstance<AuthorizationHandlerService>();
            var principal = CreateGuestPrincipal(TestDataSeed.UserId);
            return service.GetMember(TestDataSeed.UserId, clubId, principal);
        });

        result.MemberId.Should().Be(guestMemberId);
        result.Role.Should().Contain(MemberRole.Guest);
    }

    [Fact]
    public async Task GetMember_NormalSession_WithDualMembership_ReturnsClubMember()
    {
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = ctx.TestData().User;

            // User already has a ClubMember (from seed), now also add a GuestMember
            var guestMember = new GuestMember(user.Id, club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();
        });

        var result = await ScopedAsync(() =>
        {
            var service = GetInstance<AuthorizationHandlerService>();
            var principal = CreateNormalPrincipal(TestDataSeed.UserId);
            return service.GetMember(TestDataSeed.UserId, TestDataSeed.ClubId, principal);
        });

        result.MemberId.Should().Be(TestDataSeed.Member1Id);
        result.Role.Should().Contain(MemberRole.User);
    }

    [Fact]
    public async Task GetMember_GuestSession_PureGuest_ReturnsGuestMember()
    {
        var (clubId, userId, guestMemberId) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = new User("pureguestauth@test.com", "pureguestauth@test.com", "Pure", "Guest", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var guestMember = new GuestMember(user.Id, club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            return (club.Id, user.Id, guestMember.Id);
        });

        var result = await ScopedAsync(() =>
        {
            var service = GetInstance<AuthorizationHandlerService>();
            var principal = CreateGuestPrincipal(userId);
            return service.GetMember(userId, clubId, principal);
        });

        result.MemberId.Should().Be(guestMemberId);
        result.Role.Should().Contain(MemberRole.Guest);
    }

    [Fact]
    public async Task GetMember_NoPrincipal_WithDualMembership_FallsBackToFirstMember()
    {
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = ctx.TestData().User;

            var guestMember = new GuestMember(user.Id, club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();
        });

        // When no principal is passed (null), it defaults to ClubMember filter
        // and falls back to any member if not found
        var result = await ScopedAsync(() =>
        {
            var service = GetInstance<AuthorizationHandlerService>();
            return service.GetMember(TestDataSeed.UserId, TestDataSeed.ClubId);
        });

        result.MemberId.Should().NotBeNull();
        result.Role.Should().NotBeNull();
    }

    private static ClaimsPrincipal CreateGuestPrincipal(int userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("sub", userId.ToString()));
        identity.AddClaim(new Claim("guest_session", "true"));
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateNormalPrincipal(int userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("sub", userId.ToString()));
        return new ClaimsPrincipal(identity);
    }
}
