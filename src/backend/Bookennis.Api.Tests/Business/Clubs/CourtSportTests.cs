using Bookennis.Api.Business.ClubProfile;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.Business.CourtSales;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SharedSport = Bookennis.Shared.Controller.Shared.Sport;

namespace Bookennis.Api.Tests.Business.Clubs;

/// <summary>Courts with a sport and what the booking page learns about the sports of the enrollments.</summary>
public class CourtSportTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddCourt_WithASport_And_UpdateCourt_ChangesItOnlyWhenSent()
    {
        var courtId = await SendAsync(new AddCourt("Padel 1", "P1", Sport: Sport.Padel));
        (await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId))).Sport.Should().Be(Sport.Padel);

        await SendAsync(new UpdateCourt(courtId, "Padel 1", "P1", null, 3));
        (await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId))).Sport.Should().Be(Sport.Padel, "older clients don't send the sport");

        await SendAsync(new UpdateCourt(courtId, "Court 3", "C3", null, 3, Sport: Sport.Tennis));
        (await QueryAsync(ctx => ctx.Courts.SingleAsync(c => c.Id == courtId))).Sport.Should().Be(Sport.Tennis);

        var courts = await SendAsync(new GetCourts());
        courts.Courts.Single(c => c.CourtId == courtId).Sport.Should().Be(SharedSport.Tennis);
    }

    [Fact]
    public async Task GetBookingOptions_ReturnsTheSportsOfTheEnrollment()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
            ctx.MemberSeasons.Add(new MemberSeason(TestDataSeed.Member1Id, seasonId, sports: [Sport.Padel]));
            await ctx.SaveChangesAsync();
            return ctx.TestData().User.Id;
        });

        var result = await SendAsync(new GetBookingOptions(userId, TestDataSeed.ClubId));

        result.IsAllowedToBook.Should().BeTrue();
        result.EnrolledSports.Should().Equal(SharedSport.Padel);
    }

    [Fact]
    public async Task GetBookingOptions_EnrolledForEverySport_ReturnsNoSports()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
            await CourtSaleSeed.Enroll(ctx, TestDataSeed.Member1Id, seasonId);
            return ctx.TestData().User.Id;
        });

        (await SendAsync(new GetBookingOptions(userId, TestDataSeed.ClubId))).EnrolledSports.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlayers_ReturnsTheSportsOfEachPlayer()
    {
        await QueryAsync(async ctx =>
        {
            var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
            ctx.MemberSeasons.Add(new MemberSeason(TestDataSeed.Member1Id, seasonId, sports: [Sport.Tennis]));
            ctx.MemberSeasons.Add(new MemberSeason(TestDataSeed.Member2Id, seasonId));
            await ctx.SaveChangesAsync();
            return 0;
        });

        var players = (await SendAsync(new GetPlayers())).ClubMembers;

        players.Single(p => p.MemberId == TestDataSeed.Member1Id).Sports.Should().Equal(SharedSport.Tennis);
        players.Single(p => p.MemberId == TestDataSeed.Member2Id).Sports.Should().BeEmpty();
    }
}
