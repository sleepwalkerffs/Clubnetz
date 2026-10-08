using Bookennis.Api.Business.ClubProfile;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Guests;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubProfile;

public class GetBookingOptionsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetBookingOptions_ClubMember_ReturnsBookingOptionsForUser()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var result = await SendAsync(new GetBookingOptions(userId, IsGuestSession: false));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBookingOptions_GuestWithAvailableBookings_ReturnsIsAllowedToBook()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = new User("guestoptions@test.com", "guestoptions@test.com", "Guest", "Options", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var guestMember = new GuestMember(user.Id, club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            var guestCard = new GuestCard(club.Id, Guid.NewGuid(), 3, guestMember.Id);
            ctx.Add(guestCard);
            await ctx.SaveChangesAsync();

            return user.Id;
        });

        var result = await SendAsync(new GetBookingOptions(userId, IsGuestSession: true));

        result.IsAllowedToBook.Should().BeTrue();
    }

    [Fact]
    public async Task GetBookingOptions_GuestWithNoBookingsLeft_ReturnsNotAllowed()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = new User("guestnobookings@test.com", "guestnobookings@test.com", "Guest", "NoBookings", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var guestMember = new GuestMember(user.Id, club.Id);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            // 0 purchased bookings
            var guestCard = new GuestCard(club.Id, Guid.NewGuid(), 0, guestMember.Id);
            ctx.Add(guestCard);
            await ctx.SaveChangesAsync();

            return user.Id;
        });

        var result = await SendAsync(new GetBookingOptions(userId, IsGuestSession: true));

        result.IsAllowedToBook.Should().BeFalse();
    }
}
