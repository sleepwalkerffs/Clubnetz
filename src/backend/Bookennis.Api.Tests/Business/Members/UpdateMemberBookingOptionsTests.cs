using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class UpdateMemberBookingOptionsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateMemberBookingOptions_ValidOptions_UpdatesSuccessfully()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        await SendAsync(new UpdateMemberBookingOptions(memberId, AllowedSeasonIds: [], BookingsPerWeek: 5));

        var member = await QueryAsync(ctx => ctx.ClubMembers.SingleAsync(m => m.Id == memberId));
        member.BookingsPerWeek.Should().Be(5);
    }

    [Fact]
    public async Task UpdateMemberBookingOptions_EnrollsOnlyInSeasonsOfTheMembersClub()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);
        var (ownSeasonId, otherClubSeasonId) = await QueryAsync(async ctx =>
        {
            static DateOnlyInterval Period() => new(new DateOnly(2025, 4, 1), new DateOnly(2025, 10, 31));
            var otherClub = new Club("Other club", new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)), new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)), []);
            ctx.Add(otherClub);
            await ctx.SaveChangesAsync();

            var ownSeason = new Season(TestDataSeed.ClubId, Period());
            var otherClubSeason = new Season(otherClub.Id, Period());
            ctx.AddRange(ownSeason, otherClubSeason);
            await ctx.SaveChangesAsync();
            return (ownSeason.Id, otherClubSeason.Id);
        });

        await SendAsync(new UpdateMemberBookingOptions(memberId, AllowedSeasonIds: [ownSeasonId, otherClubSeasonId, int.MaxValue], BookingsPerWeek: 5));

        var seasonIds = await QueryAsync(ctx => ctx.MemberSeasons.Where(ms => ms.MemberId == memberId).Select(ms => ms.SeasonId).ToListAsync());
        seasonIds.Should().Equal(ownSeasonId);
    }

    [Fact]
    public async Task UpdateMemberBookingOptions_ExceedsClubLimit_ThrowsPreconditionException()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        var act = () => SendAsync(new UpdateMemberBookingOptions(memberId, AllowedSeasonIds: [], BookingsPerWeek: 999));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
