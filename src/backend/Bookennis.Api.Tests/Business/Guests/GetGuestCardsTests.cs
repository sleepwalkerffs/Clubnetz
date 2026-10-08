using Bookennis.Api.Business.Guests;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Guests;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Guests;

public class GetGuestCardsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetGuestCards_NoCards_ReturnsEmpty()
    {
        var result = await SendAsync(new GetGuestCards());

        result.GuestCards.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGuestCards_WithCard_ReturnsCards()
    {
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = new User("guest@test.com", "guest@test.com", "Guest", "User", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var guestMember = new GuestMember(user.Id, club.Id);
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            var guestCard = new GuestCard(club.Id, Guid.NewGuid(), 5, guestMember.Id);
            ctx.Add(guestCard);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetGuestCards());

        result.GuestCards.Should().NotBeEmpty();
        result.GuestCards[0].PurchasedBookings.Should().Be(5);
    }
}
