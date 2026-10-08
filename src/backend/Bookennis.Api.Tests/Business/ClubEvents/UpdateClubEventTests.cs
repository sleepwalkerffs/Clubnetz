using Bookennis.Api.Business.ClubEvents;
using Bookennis.Domain.ClubEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class UpdateClubEventTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateClubEvent_RemovedOption_DeletesAnswersAndKeepsRegistrations()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));
        var foodQuestion = clubEvent.Questions.Single(q => q.Text == "Staying for food?");
        var dishQuestion = clubEvent.Questions.Single(q => q.Text == "Food");
        var schnitzel = ClubEventSeed.Option(clubEvent, "Schnitzel");

        var data = ClubEventSeed.Data() with
        {
            Title = "Spring clean-up",
            Questions =
            [
                new(foodQuestion.Id, foodQuestion.Text, ClubEventQuestionSelectionMode.SingleChoice, true, false, foodQuestion.Options.Select(o => new ClubEvent.OptionData(o.Id, o.Label)).ToList()),
                new(dishQuestion.Id, "Dish", ClubEventQuestionSelectionMode.MultipleChoice, false, true, [new(schnitzel, "Schnitzel"), new(null, "Fish")]),
            ],
        };

        var result = await SendAsync(new UpdateClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id, data));

        result.Title.Should().Be("Spring clean-up");
        result.Questions[1].Text.Should().Be("Dish");
        result.Questions[1].Options.Select(o => (o.Label, o.Total)).Should().Equal(("Schnitzel", 2), ("Fish", 0));
        result.Registrations.Should().HaveCount(2);

        var answers = await QueryAsync(ctx => ctx.ClubEventRegistrationAnswers.Select(a => a.ClubEventQuestionOptionId).ToListAsync());
        answers.Should().NotContain(ClubEventSeed.Option(clubEvent, "Veggie"));
        (await QueryAsync(ctx => ctx.ClubEventQuestionOptions.CountAsync())).Should().Be(4);
    }

    [Fact]
    public async Task UpdateClubEvent_RemovedQuestion_DeletesQuestionAndAnswers()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));

        var result = await SendAsync(new UpdateClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id, ClubEventSeed.Data(withQuestions: false)));

        result.Questions.Should().BeEmpty();
        result.Registrations.Should().OnlyContain(r => r.Answers.Count == 0);
        (await QueryAsync(ctx => ctx.ClubEventRegistrationAnswers.CountAsync())).Should().Be(0);
        (await QueryAsync(ctx => ctx.ClubEventQuestions.CountAsync())).Should().Be(0);
    }
}
