using Bookennis.Api.Business.ClubEvents;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class CreateClubEventTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreateClubEvent_StoresEventWithQuestionsAndCreator()
    {
        var id = await SendAsync(new CreateClubEvent(TestDataSeed.ClubId, TestDataSeed.AdminId, ClubEventSeed.Data()));

        var clubEvent = await QueryAsync(ctx => ctx.ClubEvents
            .Include(e => e.Questions).ThenInclude(q => q.Options)
            .SingleAsync(e => e.Id == id));

        clubEvent.ClubId.Should().Be(TestDataSeed.ClubId);
        clubEvent.CreatedByMemberId.Should().Be(TestDataSeed.Member2Id);
        clubEvent.Title.Should().Be("Work effort");
        clubEvent.Questions.OrderBy(q => q.SortOrder).Select(q => q.Text).Should().Equal("Staying for food?", "Food");
        clubEvent.Questions.Single(q => q.Text == "Food").Options.Select(o => o.Label).Should().BeEquivalentTo(["Schnitzel", "Veggie"]);
    }

    [Fact]
    public async Task CreateClubEvent_UserWithoutMembership_StoresEventWithoutCreator()
    {
        var id = await SendAsync(new CreateClubEvent(TestDataSeed.ClubId, 999_999, ClubEventSeed.Data(withQuestions: false)));

        var clubEvent = await QueryAsync(ctx => ctx.ClubEvents.SingleAsync(e => e.Id == id));
        clubEvent.CreatedByMemberId.Should().BeNull();
    }

    [Fact]
    public async Task CreateClubEvent_EmptyTitle_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new CreateClubEvent(TestDataSeed.ClubId, TestDataSeed.AdminId, ClubEventSeed.Data() with { Title = "" }));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubEvent.ErrorCode.ClubEventTitleRequired));
    }
}
