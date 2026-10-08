using Bookennis.Api.Business.ClubEvents;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class UnregisterFromClubEventTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UnregisterFromClubEvent_RemovesOwnRegistration()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));

        var result = await SendAsync(new UnregisterFromClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        result.MyRegistration.Should().BeNull();
        result.TotalHeadCount.Should().Be(1);
        var memberIds = await QueryAsync(ctx => ctx.ClubEventRegistrations.Select(r => r.MemberId).ToListAsync());
        memberIds.Should().Equal(TestDataSeed.Member2Id);
        (await QueryAsync(ctx => ctx.ClubEventRegistrationAnswers.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task UnregisterFromClubEvent_NotRegistered_ThrowsPreconditionException()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx));

        var act = () => SendAsync(new UnregisterFromClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubEvent.ErrorCode.ClubEventNotRegistered));
    }
}
