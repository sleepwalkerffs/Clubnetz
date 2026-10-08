using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.ClubEvents;

public class ClubEventTests
{
    private static readonly DateOnly EventDate = new(2026, 10, 10);
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    // Option ids assigned by CreateEventWithQuestions
    private const int FoodYes = 11;
    private const int FoodNo = 12;
    private const int Schnitzel = 21;
    private const int Veggie = 22;

    [Fact]
    public void Constructor_EmptyTitle_Throws()
    {
        var act = () => new ClubEvent(1, 1, Data() with { Title = "  " });

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventTitleRequired);
    }

    [Fact]
    public void Constructor_EndDateBeforeStartDate_Throws()
    {
        var act = () => new ClubEvent(1, 1, Data() with { EndDate = EventDate.AddDays(-1) });

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidDateRange);
    }

    [Fact]
    public void Constructor_EndTimeBeforeStartTimeOnSameDay_Throws()
    {
        var act = () => new ClubEvent(1, 1, Data() with { StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(9, 0) });

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidTimeRange);
    }

    [Fact]
    public void Constructor_EndTimeBeforeStartTimeOnMultiDayEvent_IsValid()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { EndDate = EventDate.AddDays(1), StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(9, 0) });

        clubEvent.EndTime.Should().Be(new TimeOnly(9, 0));
    }

    [Fact]
    public void Constructor_EndDateEqualsStartDate_IsStoredAsSingleDay()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { EndDate = EventDate });

        clubEvent.EndDate.Should().BeNull();
    }

    [Fact]
    public void Constructor_InvalidMaxParticipants_Throws()
    {
        var act = () => new ClubEvent(1, 1, Data() with { MaxParticipants = 0 });

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidMaxParticipants);
    }

    [Fact]
    public void Constructor_QuestionWithoutOptions_Throws()
    {
        var act = () => new ClubEvent(1, 1, Data() with { Questions = [new ClubEvent.QuestionData(null, "Food?", ClubEventQuestionSelectionMode.SingleChoice, false, false, [])] });

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventQuestionNeedsOptions);
    }

    [Fact]
    public void Constructor_AllowQuantitiesOnSingleChoice_IsIgnored()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { Questions = [new ClubEvent.QuestionData(null, "Food?", ClubEventQuestionSelectionMode.SingleChoice, false, true, [new(null, "Yes")])] });

        clubEvent.Questions.Single().AllowQuantities.Should().BeFalse();
    }

    [Fact]
    public void Register_CreatesRegistrationWithAnswers()
    {
        var clubEvent = CreateEventWithQuestions();

        clubEvent.Register(5, 3, [new(FoodYes, 1), new(Schnitzel, 2), new(Veggie, 1)], " Bringing a cake ", Now);

        var registration = clubEvent.Registrations.Should().ContainSingle().Subject;
        registration.MemberId.Should().Be(5);
        registration.HeadCount.Should().Be(3);
        registration.Comment.Should().Be("Bringing a cake");
        registration.RegisteredAt.Should().Be(Now);
        registration.Answers.Select(a => (a.ClubEventQuestionOptionId, a.Quantity)).Should().BeEquivalentTo([(FoodYes, 1), (Schnitzel, 2), (Veggie, 1)]);
        clubEvent.TotalHeadCount.Should().Be(3);
    }

    [Fact]
    public void Register_Again_UpdatesExistingRegistration()
    {
        var clubEvent = CreateEventWithQuestions();
        clubEvent.Register(5, 3, [new(FoodYes, 1), new(Schnitzel, 2)], null, Now);

        clubEvent.Register(5, 1, [new(FoodNo, 1)], null, Now.AddHours(1));

        var registration = clubEvent.Registrations.Should().ContainSingle().Subject;
        registration.HeadCount.Should().Be(1);
        registration.RegisteredAt.Should().Be(Now);
        registration.Answers.Select(a => a.ClubEventQuestionOptionId).Should().Equal(FoodNo);
    }

    [Fact]
    public void Register_QuantityWithoutAllowQuantities_IsNormalizedToOne()
    {
        var clubEvent = CreateEventWithQuestions();

        clubEvent.Register(5, 3, [new(FoodYes, 3)], null, Now);

        clubEvent.Registrations.Single().Answers.Single().Quantity.Should().Be(1);
    }

    [Fact]
    public void Register_QuantityExceedsHeadCount_Throws()
    {
        var clubEvent = CreateEventWithQuestions();

        var act = () => clubEvent.Register(5, 2, [new(FoodYes, 1), new(Schnitzel, 3)], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidAnswer);
    }

    [Fact]
    public void Register_UnlimitedQuantity_MayExceedHeadCount()
    {
        var clubEvent = CreateEventWithQuestions(limitQuantityToHeadCount: false);

        clubEvent.Register(5, 2, [new(FoodYes, 1), new(Schnitzel, 6)], null, Now);

        clubEvent.Registrations.Single().Answers.Single(a => a.ClubEventQuestionOptionId == Schnitzel).Quantity.Should().Be(6);
    }

    [Fact]
    public void Register_UnlimitedQuantityAboveMaximum_Throws()
    {
        var clubEvent = CreateEventWithQuestions(limitQuantityToHeadCount: false);

        var act = () => clubEvent.Register(5, 2, [new(FoodYes, 1), new(Schnitzel, ClubEvent.MaxQuantity + 1)], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidAnswer);
    }

    [Fact]
    public void Constructor_LimitQuantityWithoutQuantities_IsAlwaysLimited()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with
        {
            Questions = [new(null, "Drinks", ClubEventQuestionSelectionMode.MultipleChoice, false, false, [new(null, "Beer")], LimitQuantityToHeadCount: false)],
        });

        clubEvent.Questions.Single().LimitQuantityToHeadCount.Should().BeTrue();
    }

    [Fact]
    public void Register_TwoOptionsOfSingleChoiceQuestion_Throws()
    {
        var clubEvent = CreateEventWithQuestions();

        var act = () => clubEvent.Register(5, 1, [new(FoodYes, 1), new(FoodNo, 1)], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidAnswer);
    }

    [Fact]
    public void Register_UnknownOption_Throws()
    {
        var clubEvent = CreateEventWithQuestions();

        var act = () => clubEvent.Register(5, 1, [new(FoodYes, 1), new(999, 1)], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidAnswer);
    }

    [Fact]
    public void Register_RequiredQuestionNotAnswered_Throws()
    {
        var clubEvent = CreateEventWithQuestions();

        var act = () => clubEvent.Register(5, 1, [new(Schnitzel, 1)], null, Now);

        act.Should().Throw<PreconditionException>()
           .Which.ErrorCode.Should().Be(nameof(ClubEvent.ErrorCode.ClubEventRequiredQuestionNotAnswered));
    }

    [Fact]
    public void Register_RegistrationDisabled_Throws()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { RegistrationEnabled = false });

        var act = () => clubEvent.Register(5, 1, [], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventRegistrationDisabled);
    }

    [Fact]
    public void Register_AfterDeadline_Throws()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { RegistrationDeadline = Now.AddMinutes(-1) });

        var act = () => clubEvent.Register(5, 1, [], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventRegistrationClosed);
    }

    [Fact]
    public void Register_AfterEventIsOver_Throws()
    {
        var clubEvent = new ClubEvent(1, 1, Data());

        var act = () => clubEvent.Register(5, 1, [], null, new DateTimeOffset(EventDate.AddDays(1), TimeOnly.MinValue, TimeSpan.Zero));

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventRegistrationClosed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ClubEvent.MaxHeadCount + 1)]
    public void Register_InvalidHeadCount_Throws(int headCount)
    {
        var clubEvent = new ClubEvent(1, 1, Data());

        var act = () => clubEvent.Register(5, headCount, [], null, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventInvalidHeadCount);
    }

    [Fact]
    public void Register_ExceedsCapacity_Throws()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { MaxParticipants = 5 });
        clubEvent.Register(5, 3, [], null, Now);

        var act = () => clubEvent.Register(6, 3, [], null, Now);

        act.Should().Throw<PreconditionException>()
           .Which.Should().Match<PreconditionException>(e => e.ErrorCode == nameof(ClubEvent.ErrorCode.ClubEventFull) && e.ErrorDetails!.Single() == "2");
    }

    [Fact]
    public void Register_UpdateOwnRegistrationWithinCapacity_Succeeds()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { MaxParticipants = 5 });
        clubEvent.Register(5, 3, [], null, Now);
        clubEvent.Register(6, 1, [], null, Now);

        clubEvent.Register(5, 4, [], null, Now);

        clubEvent.TotalHeadCount.Should().Be(5);
        clubEvent.IsFull.Should().BeTrue();
    }

    [Fact]
    public void Unregister_RemovesRegistration()
    {
        var clubEvent = new ClubEvent(1, 1, Data());
        clubEvent.Register(5, 2, [], null, Now);

        clubEvent.Unregister(5, Now);

        clubEvent.Registrations.Should().BeEmpty();
    }

    [Fact]
    public void Unregister_NotRegistered_Throws()
    {
        var clubEvent = new ClubEvent(1, 1, Data());

        var act = () => clubEvent.Unregister(5, Now);

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventNotRegistered);
    }

    [Fact]
    public void Unregister_AfterDeadline_Throws()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { RegistrationDeadline = Now.AddDays(1) });
        clubEvent.Register(5, 2, [], null, Now);

        var act = () => clubEvent.Unregister(5, Now.AddDays(2));

        ShouldThrow(act, ClubEvent.ErrorCode.ClubEventRegistrationClosed);
    }

    [Fact]
    public void RemoveRegistration_AfterDeadline_Succeeds()
    {
        var clubEvent = new ClubEvent(1, 1, Data() with { RegistrationDeadline = Now.AddDays(1) });
        var registration = clubEvent.Register(5, 2, [], null, Now);
        registration.SetId(77);

        clubEvent.RemoveRegistration(77);

        clubEvent.Registrations.Should().BeEmpty();
    }

    [Fact]
    public void Update_RemovedOption_RemovesAnswers()
    {
        var clubEvent = CreateEventWithQuestions();
        clubEvent.Register(5, 3, [new(FoodYes, 1), new(Schnitzel, 2), new(Veggie, 1)], null, Now);

        clubEvent.Update(Data() with
        {
            Questions =
            [
                new(10, "Staying for food?", ClubEventQuestionSelectionMode.SingleChoice, true, false, [new(FoodYes, "Yes"), new(FoodNo, "No")]),
                new(20, "Food", ClubEventQuestionSelectionMode.MultipleChoice, false, true, [new(Schnitzel, "Schnitzel")]),
            ],
        });

        clubEvent.Registrations.Single().Answers.Select(a => a.ClubEventQuestionOptionId).Should().BeEquivalentTo([FoodYes, Schnitzel]);
        clubEvent.Questions.Single(q => q.Id == 20).Options.Select(o => o.Label).Should().Equal("Schnitzel");
    }

    [Fact]
    public void Update_RemovedQuestion_RemovesAnswersAndKeepsRegistration()
    {
        var clubEvent = CreateEventWithQuestions();
        clubEvent.Register(5, 3, [new(FoodYes, 1), new(Schnitzel, 2)], null, Now);

        clubEvent.Update(Data() with
        {
            Questions = [new(10, "Staying for food?", ClubEventQuestionSelectionMode.SingleChoice, true, false, [new(FoodYes, "Yes"), new(FoodNo, "No")])],
        });

        clubEvent.Questions.Should().ContainSingle();
        clubEvent.Registrations.Single().Answers.Select(a => a.ClubEventQuestionOptionId).Should().Equal(FoodYes);
    }

    [Fact]
    public void Update_DisableRegistration_KeepsRegistrations()
    {
        var clubEvent = new ClubEvent(1, 1, Data());
        clubEvent.Register(5, 2, [], null, Now);

        clubEvent.Update(Data() with { RegistrationEnabled = false });

        clubEvent.Registrations.Should().ContainSingle();
        clubEvent.IsRegistrationOpen(Now).Should().BeFalse();
    }

    private static ClubEvent.EventData Data() => new(
        "Work effort",
        "Clean the courts",
        "Clubhouse",
        ClubEventCategory.WorkEffort,
        EventDate,
        null,
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        true,
        null,
        null,
        []);

    private static ClubEvent CreateEventWithQuestions(bool limitQuantityToHeadCount = true)
    {
        var clubEvent = new ClubEvent(1, 1, Data() with
        {
            Questions =
            [
                new(null, "Staying for food?", ClubEventQuestionSelectionMode.SingleChoice, true, false, [new(null, "Yes"), new(null, "No")]),
                new(null, "Food", ClubEventQuestionSelectionMode.MultipleChoice, false, true, [new(null, "Schnitzel"), new(null, "Veggie")], limitQuantityToHeadCount),
            ],
        });

        var food = clubEvent.Questions[0];
        food.SetId(10);
        food.Options[0].SetId(FoodYes);
        food.Options[1].SetId(FoodNo);

        var dishes = clubEvent.Questions[1];
        dishes.SetId(20);
        dishes.Options[0].SetId(Schnitzel);
        dishes.Options[1].SetId(Veggie);

        return clubEvent;
    }

    private static void ShouldThrow(Action act, ClubEvent.ErrorCode errorCode)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(errorCode.ToString());

    private static void ShouldThrow<T>(Func<T> act, ClubEvent.ErrorCode errorCode)
        => act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(errorCode.ToString());
}
