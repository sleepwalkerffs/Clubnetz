using Bookennis.Api.Business.ClubEvents;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class GetClubEventTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubEvent_ReturnsDetailsWithTotalsAndOwnRegistration()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx, maxParticipants: 4));

        var result = await SendAsync(new GetClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        result.Title.Should().Be("Work effort");
        result.CreatedByName.Should().Be("ad min");
        result.TotalHeadCount.Should().Be(4);
        result.IsFull.Should().BeTrue();
        result.IsRegistrationOpen.Should().BeTrue();
        result.IsOver.Should().BeFalse();

        var options = result.Questions.SelectMany(q => q.Options).ToDictionary(o => o.Label, o => o.Total);
        options.Should().BeEquivalentTo(new Dictionary<string, int> { ["Yes"] = 1, ["No"] = 1, ["Schnitzel"] = 2, ["Veggie"] = 1 });

        result.Registrations.Select(r => (r.FirstName, r.HeadCount)).Should().Equal(("us", 3), ("ad", 1));
        result.MyRegistration.Should().NotBeNull();
        result.MyRegistration!.MemberId.Should().Be(TestDataSeed.Member1Id);
        result.MyRegistration.Comment.Should().Be("Bringing a cake");
        result.MyRegistration.Answers.Should().Contain(a => a.OptionId == ClubEventSeed.Option(clubEvent, "Schnitzel") && a.Quantity == 2);
    }

    [Fact]
    public async Task GetClubEvent_PastEvent_IsOverAndClosed()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2))));

        var result = await SendAsync(new GetClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        result.IsOver.Should().BeTrue();
        result.IsRegistrationOpen.Should().BeFalse();
        result.MyRegistration.Should().BeNull();
    }

    [Fact]
    public async Task GetClubEvent_RendersDescriptionAsSanitizedMarkdown()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data() with
        {
            Description = "**Bring** gloves\nand a rake <script>alert(1)</script>\n\n- [Map](https://example.com) ![Image](https://example.com/a.png)",
        }));

        var result = await SendAsync(new GetClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        result.Description.Should().StartWith("**Bring** gloves");
        result.DescriptionHtml.Should().Contain("<strong>Bring</strong> gloves<br>").And.Contain("<li>");
        result.DescriptionHtml.Should().Contain("href=\"https://example.com\"").And.Contain("target=\"_blank\"");
        result.DescriptionHtml.Should().NotContain("<script").And.NotContain("<img");
    }

    [Fact]
    public async Task GetClubEvent_WithoutDescription_HasNoDescriptionHtml()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data() with { Description = null }));

        var result = await SendAsync(new GetClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        result.DescriptionHtml.Should().BeNull();
    }

    [Fact]
    public async Task GetClubEvent_OtherClub_ThrowsNotFound()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx));

        var act = () => SendAsync(new GetClubEvent(TestDataSeed.ClubId + 1, TestDataSeed.UserId, clubEvent.Id));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetClubEvent_RegistrationDisabled_IsNotOpen()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data() with { RegistrationEnabled = false }));

        var result = await SendAsync(new GetClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id));

        result.RegistrationEnabled.Should().BeFalse();
        result.IsRegistrationOpen.Should().BeFalse();
        result.Category.Should().Be(Bookennis.Shared.Controller.ClubEvents.ClubEventCategory.WorkEffort);
    }
}
