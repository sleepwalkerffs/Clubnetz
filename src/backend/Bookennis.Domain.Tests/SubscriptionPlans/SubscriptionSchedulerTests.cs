using Bookennis.Domain.SubscriptionPlans;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.SubscriptionPlans;

public class SubscriptionSchedulerTests
{
    private static readonly DateOnly FirstMonday = new(2026, 10, 5);

    private readonly SubscriptionScheduler scheduler = new();

    [Fact]
    public void Schedule_EqualPercentages_CountsDifferByAtMostOne()
    {
        var input = CreateInput(participantCount: 6, weekCount: 20, playersPerWeek: 4);

        var result = scheduler.Schedule(input);

        var counts = Counts(result, input);
        counts.Values.Sum().Should().Be(80);
        (counts.Values.Max() - counts.Values.Min()).Should().BeLessThanOrEqualTo(1);
        result.Assignments.Values.Should().AllSatisfy(players => players.Should().HaveCount(4).And.OnlyHaveUniqueItems());
    }

    [Fact]
    public void Schedule_HalfPercentage_PlaysAboutHalfAsOften()
    {
        var participants = Enumerable.Range(1, 5)
            .Select(id => new SubscriptionScheduler.Participant(id, id == 1 ? 50 : 100, new HashSet<DateOnly>()))
            .ToList();
        var input = new SubscriptionScheduler.Input(Weeks(18), participants, 2, Seed: 1);

        var counts = Counts(scheduler.Schedule(input), input);

        // 36 slots split 1 : 2 : 2 : 2 : 2 => 4 vs. 8 games
        counts[1].Should().BeInRange(3, 5);
        counts.Where(c => c.Key != 1).Should().AllSatisfy(c => c.Value.Should().BeInRange(7, 9));
    }

    [Fact]
    public void Schedule_UnavailableWeeks_AreNeverAssigned()
    {
        var weeks = Weeks(10);
        var unavailable = new HashSet<DateOnly> { weeks[0], weeks[3], weeks[4] };
        var participants = Enumerable.Range(1, 5)
            .Select(id => new SubscriptionScheduler.Participant(id, 100, id == 2 ? unavailable : new HashSet<DateOnly>()))
            .ToList();
        var input = new SubscriptionScheduler.Input(weeks, participants, 2, Seed: 3);

        var result = scheduler.Schedule(input);

        foreach (var week in unavailable)
            result.Assignments[week].Should().NotContain(2);
    }

    [Fact]
    public void Schedule_PairsAreBalanced()
    {
        var input = CreateInput(participantCount: 8, weekCount: 20, playersPerWeek: 4);

        var result = scheduler.Schedule(input);

        var pairCounts = new Dictionary<(int, int), int>();
        foreach (var i in input.Participants)
            foreach (var j in input.Participants.Where(p => p.Id > i.Id))
                pairCounts[(i.Id, j.Id)] = 0;

        foreach (var players in result.Assignments.Values)
            foreach (var i in players)
                foreach (var j in players.Where(p => p > i))
                    pairCounts[(i, j)]++;

        // 20 weeks * 6 pairs per week / 28 pairs => ~4.3 per pair
        (pairCounts.Values.Max() - pairCounts.Values.Min()).Should().BeLessThanOrEqualTo(2);
    }

    [Theory]
    [InlineData(6, 20, 4, 4, 2)]  // plays 2 of 3 weeks
    [InlineData(5, 26, 4, 5, 1)]  // rests every 5th week
    [InlineData(8, 24, 2, 1, 5)]  // plays every 4th week
    [InlineData(10, 22, 4, 2, 4)] // plays every 2.5th week
    [InlineData(12, 26, 4, 2, 4)] // plays every 3rd week
    public void Schedule_SpreadsGamesEvenly(int participantCount, int weekCount, int playersPerWeek, int maxStreak, int maxBreak)
    {
        var input = CreateInput(participantCount, weekCount, playersPerWeek);

        var result = scheduler.Schedule(input);

        foreach (var participant in input.Participants)
        {
            var played = input.Weeks.Select(w => result.Assignments[w].Contains(participant.Id)).ToList();
            LongestRun(played, true).Should().BeLessThanOrEqualTo(maxStreak, $"participant {participant.Id} should not play too many weeks in a row");
            LongestRun(played, false).Should().BeLessThanOrEqualTo(maxBreak, $"participant {participant.Id} should not pause too long");
        }
    }

    [Fact]
    public void Schedule_AfterUnavailableWeeks_ParticipantIsNotOverloaded()
    {
        var weeks = Weeks(20);
        var away = weeks.Take(6).ToHashSet();
        var participants = Enumerable.Range(1, 8)
            .Select(id => new SubscriptionScheduler.Participant(id, 100, id == 1 ? away : new HashSet<DateOnly>()))
            .ToList();
        var input = new SubscriptionScheduler.Input(weeks, participants, 4, Seed: 11);

        var result = scheduler.Schedule(input);

        // Participant 1 has 14 available weeks and should play about every other one, not every week to catch up
        var played = weeks.Skip(6).Select(w => result.Assignments[w].Contains(1)).ToList();
        LongestRun(played, true).Should().BeLessThanOrEqualTo(3);
        LongestRun(played, false).Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Schedule_SameSeed_ReturnsSameResult()
    {
        var input = CreateInput(participantCount: 7, weekCount: 15, playersPerWeek: 4);

        var first = scheduler.Schedule(input);
        var second = scheduler.Schedule(input);

        second.Assignments.Should().BeEquivalentTo(first.Assignments);
    }

    [Fact]
    public void Schedule_NotEnoughAvailablePlayers_FlagsUnderstaffedWeek()
    {
        var weeks = Weeks(4);
        var participants = Enumerable.Range(1, 4)
            .Select(id => new SubscriptionScheduler.Participant(id, 100, id <= 2 ? new HashSet<DateOnly> { weeks[1] } : new HashSet<DateOnly>()))
            .ToList();
        var input = new SubscriptionScheduler.Input(weeks, participants, 4, Seed: 5);

        var result = scheduler.Schedule(input);

        result.UnderstaffedWeeks.Should().Equal(weeks[1]);
        result.Assignments[weeks[1]].Should().BeEquivalentTo([3, 4]);
        result.Assignments[weeks[0]].Should().HaveCount(4);
    }

    [Fact]
    public void CalculateExpectations_CappedParticipant_HandsSlotsToOthers()
    {
        var weeks = Weeks(10);
        var participants = new List<SubscriptionScheduler.Participant>
        {
            new(1, 100, weeks.Skip(2).ToHashSet()), // only 2 weeks available
            new(2, 100, new HashSet<DateOnly>()),
            new(3, 100, new HashSet<DateOnly>()),
        };

        var expectations = SubscriptionScheduler.CalculateExpectations(weeks, participants, 2);

        expectations.GetTarget(1).Should().Be(2);
        expectations.GetTarget(2).Should().Be(9);
        expectations.GetTarget(3).Should().Be(9);
    }

    private static SubscriptionScheduler.Input CreateInput(int participantCount, int weekCount, int playersPerWeek)
        => new(
            Weeks(weekCount),
            Enumerable.Range(1, participantCount).Select(id => new SubscriptionScheduler.Participant(id, 100, new HashSet<DateOnly>())).ToList(),
            playersPerWeek,
            Seed: 42);

    private static int LongestRun(List<bool> played, bool value)
    {
        int longest = 0, current = 0;
        foreach (var p in played)
        {
            current = p == value ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    private static List<DateOnly> Weeks(int count)
        => Enumerable.Range(0, count).Select(i => FirstMonday.AddDays(7 * i)).ToList();

    private static Dictionary<int, int> Counts(SubscriptionScheduler.Result result, SubscriptionScheduler.Input input)
        => input.Participants.ToDictionary(p => p.Id, p => result.Assignments.Values.Count(players => players.Contains(p.Id)));
}
