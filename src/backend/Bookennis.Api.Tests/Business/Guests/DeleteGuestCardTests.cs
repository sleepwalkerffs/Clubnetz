using Bookennis.Api.Business.Guests;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Guests;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Guests;

public class DeleteGuestCardTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteGuestCard_RemovesCard()
    {
        var guestCardId = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var user = new User("guestdelete@test.com", "guestdelete@test.com", "Guest", "Delete", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var guestMember = new GuestMember(user.Id, club.Id);
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();

            var guestCard = new GuestCard(club.Id, Guid.NewGuid(), 3, guestMember.Id);
            ctx.Add(guestCard);
            await ctx.SaveChangesAsync();
            return guestCard.Id;
        });

        await SendAsync(new DeleteGuestCard(guestCardId));

        var exists = await QueryAsync(ctx => ctx.GuestCards.AnyAsync(g => g.Id == guestCardId));
        exists.Should().BeFalse();
    }
}
