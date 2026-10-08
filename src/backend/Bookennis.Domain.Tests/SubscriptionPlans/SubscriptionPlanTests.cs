using Bookennis.Domain.Exceptions;
using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Domain.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.SubscriptionPlans;

public class SubscriptionPlanTests
{
    private static readonly DateOnly Start = new(2026, 10, 1); // Thursday, week 40
    private static readonly DateOnly End = new(2026, 10, 31);  // Saturday, week 44
    private static readonly DateOnly Week41 = new(2026, 10, 5);

    [Fact]
    public void Constructor_EndBeforeStart_Throws()
    {
        var act = () => new SubscriptionPlan(1, 1, "Winter", End, Start, 4);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanInvalidDateRange));
    }

    [Fact]
    public void Constructor_RangeLongerThanAYear_Throws()
    {
        var act = () => new SubscriptionPlan(1, 1, "Winter", Start, Start.AddYears(2), 4);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanDateRangeTooLong));
    }

    [Fact]
    public void GetWeeks_IncludesPartialFirstAndLastWeek()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 4);

        plan.GetWeeks().Select(w => w.WeekNumber).Should().Equal(40, 41, 42, 43, 44);
    }

    [Fact]
    public void Update_DuplicateParticipantNames_Throws()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);

        var act = () => plan.Update("Winter", Start, End, 2, [], [Participant("Anna"), Participant(" anna ")]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanDuplicateParticipantName));
    }

    [Fact]
    public void Update_InvalidPercentage_Throws()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);

        var act = () => plan.Update("Winter", Start, End, 2, [], [Participant("Anna", 0)]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanInvalidPercentage));
    }

    [Fact]
    public void Update_NormalizesWeeksToMondaysWithinRange()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);

        plan.Update("Winter", Start, End, 2, [new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 12), new DateOnly(2027, 1, 4)], []);

        plan.ExcludedWeeks.Should().Equal(new DateOnly(2026, 10, 12));
        plan.GetActiveWeeks().Should().HaveCount(4);
    }

    [Fact]
    public void Update_RenamingParticipant_KeepsSchedule()
    {
        var plan = CreatePlanWithSchedule();
        var (anna, ben) = (plan.Participants[0], plan.Participants[1]);

        plan.Update("Winter 26", Start, End, 2, [], [Participant("Anna B.", 100, anna.Id), Participant("Ben", 100, ben.Id)]);

        plan.HasSchedule.Should().BeTrue();
        plan.Participants[0].Name.Should().Be("Anna B.");
    }

    [Fact]
    public void Update_ChangingPercentage_ClearsSchedule()
    {
        var plan = CreatePlanWithSchedule();
        var (anna, ben) = (plan.Participants[0], plan.Participants[1]);

        plan.Update("Winter", Start, End, 2, [], [Participant("Anna", 50, anna.Id), Participant("Ben", 100, ben.Id)]);

        plan.HasSchedule.Should().BeFalse();
    }

    [Fact]
    public void SetSchedule_UnavailableParticipant_Throws()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);
        plan.Update("Winter", Start, End, 2, [], [Participant("Anna", unavailable: [Week41]), Participant("Ben")]);
        SetIds(plan);

        var act = () => plan.SetSchedule([new SubscriptionPlan.WeekAssignmentData(Week41, [1, 2])]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanInvalidAssignment));
    }

    [Fact]
    public void SetSchedule_ExcludedWeek_Throws()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);
        plan.Update("Winter", Start, End, 2, [Week41], [Participant("Anna"), Participant("Ben")]);
        SetIds(plan);

        var act = () => plan.SetSchedule([new SubscriptionPlan.WeekAssignmentData(Week41, [1, 2])]);

        act.Should().Throw<PreconditionException>();
    }

    [Fact]
    public void SetSchedule_TooManyPlayers_Throws()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);
        plan.Update("Winter", Start, End, 2, [], [Participant("Anna"), Participant("Ben"), Participant("Carl")]);
        SetIds(plan);

        var act = () => plan.SetSchedule([new SubscriptionPlan.WeekAssignmentData(Week41, [1, 2, 3])]);

        act.Should().Throw<PreconditionException>();
    }

    [Fact]
    public void EnsureSchedulable_FewerParticipantsThanPlayersPerWeek_Throws()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 4);
        plan.Update("Winter", Start, End, 4, [], [Participant("Anna"), Participant("Ben")]);

        var act = plan.EnsureSchedulable;

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(SubscriptionPlan.ErrorCode.SubscriptionPlanNotEnoughParticipants));
    }

    private static SubscriptionPlan CreatePlanWithSchedule()
    {
        var plan = new SubscriptionPlan(1, 1, "Winter", Start, End, 2);
        plan.Update("Winter", Start, End, 2, [], [Participant("Anna"), Participant("Ben")]);
        SetIds(plan);
        plan.SetSchedule([new SubscriptionPlan.WeekAssignmentData(Week41, [1, 2])]);
        return plan;
    }

    private static void SetIds(SubscriptionPlan plan)
    {
        for (var i = 0; i < plan.Participants.Count; i++)
            plan.Participants[i].SetId(i + 1);
    }

    private static SubscriptionPlan.ParticipantData Participant(string name, int percentage = 100, int? id = null, IReadOnlyCollection<DateOnly>? unavailable = null)
        => new(id, name, percentage, 0, unavailable ?? []);
}
