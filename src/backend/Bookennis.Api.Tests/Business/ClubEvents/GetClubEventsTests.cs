using Bookennis.Api.Business.ClubEvents;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class GetClubEventsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubEvents_ReturnsEventsOverlappingRangeWithTotals()
    {
        var date = ClubEventSeed.EventDate;
        var withRegistrations = await QueryAsync(async ctx => (await ClubEventSeed.SeedEventWithRegistrations(ctx, maxParticipants: 20)).Id);
        var multiDay = await QueryAsync(async ctx => (await ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(date.AddDays(-3)) with { Title = "Camp", EndDate = date.AddDays(-1) })).Id);
        await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(date.AddDays(-10)) with { Title = "Too early" }));
        await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(date.AddDays(1)) with { Title = "Too late" }));

        var result = await SendAsync(new GetClubEvents(TestDataSeed.ClubId, TestDataSeed.UserId, date.AddDays(-1), date));

        result.Events.Select(e => e.Id).Should().Equal(multiDay, withRegistrations);

        var summary = result.Events.Single(e => e.Id == withRegistrations);
        summary.TotalHeadCount.Should().Be(4);
        summary.RegistrationCount.Should().Be(2);
        summary.MaxParticipants.Should().Be(20);
        summary.MyHeadCount.Should().Be(3);

        result.Events.Single(e => e.Id == multiDay).MyHeadCount.Should().BeNull();
    }

    [Fact]
    public async Task GetClubEvents_UserWithoutMembership_ReturnsEventsWithoutOwnRegistration()
    {
        await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));

        var result = await SendAsync(new GetClubEvents(TestDataSeed.ClubId, 999_999, ClubEventSeed.EventDate, ClubEventSeed.EventDate));

        result.Events.Should().ContainSingle().Which.MyHeadCount.Should().BeNull();
    }
}
