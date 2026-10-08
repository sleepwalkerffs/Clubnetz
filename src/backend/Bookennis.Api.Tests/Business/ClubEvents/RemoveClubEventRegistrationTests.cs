using Bookennis.Api.Business.ClubEvents;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class RemoveClubEventRegistrationTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task RemoveClubEventRegistration_RemovesRegistrationOfOtherMember()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));
        var registrationId = clubEvent.Registrations.Single(r => r.MemberId == TestDataSeed.Member1Id).Id;

        var result = await SendAsync(new RemoveClubEventRegistration(TestDataSeed.ClubId, TestDataSeed.AdminId, clubEvent.Id, registrationId));

        result.Registrations.Select(r => r.MemberId).Should().Equal(TestDataSeed.Member2Id);
        result.MyRegistration.Should().NotBeNull();
        result.Questions.SelectMany(q => q.Options).Single(o => o.Label == "Schnitzel").Total.Should().Be(0);
    }
}
