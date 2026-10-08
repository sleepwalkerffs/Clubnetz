using Bookennis.Api.Business.ClubEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class DeleteClubEventTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteClubEvent_DeletesEventWithRegistrations()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));

        await SendAsync(new DeleteClubEvent(TestDataSeed.ClubId, clubEvent.Id));

        (await QueryAsync(ctx => ctx.ClubEvents.AnyAsync())).Should().BeFalse();
        (await QueryAsync(ctx => ctx.ClubEventRegistrations.AnyAsync())).Should().BeFalse();
        (await QueryAsync(ctx => ctx.ClubEventRegistrationAnswers.AnyAsync())).Should().BeFalse();
    }
}
