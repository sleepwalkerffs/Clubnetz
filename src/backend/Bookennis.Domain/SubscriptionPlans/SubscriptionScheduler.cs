using Bookennis.Domain.Base;

namespace Bookennis.Domain.SubscriptionPlans;

public interface ISubscriptionScheduler
{
    public SubscriptionScheduler.Result Schedule(SubscriptionScheduler.Input input);
}

/// <summary>
/// Distributes participants over weeks so that
/// 1. every participant plays proportionally to their percentage (capped by their availability) and
/// 2. every pair of participants shares a week about equally often (relative to their percentages) and
/// 3. every participant's games are spread evenly over the season (no long streaks or long breaks).
/// A greedy construction is refined with simulated annealing; the seed makes the result deterministic.
/// </summary>
public class SubscriptionScheduler : ISubscriptionScheduler, IDomainService
{
    public record Participant(int Id, double Weight, IReadOnlySet<DateOnly> UnavailableWeeks);

    public record Input(IReadOnlyList<DateOnly> Weeks, IReadOnlyList<Participant> Participants, int PlayersPerWeek, int Seed);

    public record Result(IReadOnlyDictionary<DateOnly, IReadOnlyList<int>> Assignments, IReadOnlyList<DateOnly> UnderstaffedWeeks);

    /// <summary>Fair targets derived from the inputs: games per participant and shared weeks per pair.</summary>
    public sealed class Expectations
    {
        private readonly Dictionary<int, int> indexById;
        private readonly double[] targets;
        private readonly double[,] pairs;

        internal Expectations(Problem problem)
        {
            indexById = problem.Ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            targets = problem.Targets;
            pairs = problem.ExpectedPairs;
        }

        public double GetTarget(int participantId) => targets[indexById[participantId]];

        public double GetExpectedPairCount(int participantId1, int participantId2)
            => pairs[indexById[participantId1], indexById[participantId2]];
    }

    private const double CountWeight = 50;
    private const double PairWeight = 1;
    // Tuned with 20 seeds for 5-12 participants: games spread evenly (no streaks or breaks of more than ~2x the ideal)
    // without splitting the group into fixed halves that never play together.
    private const double SpacingWeight = 3;
    private const int MaxSpacingWindow = 12;
    // Games a stretch of weeks may deviate from the fair share without cost; strict evenness (0) would force rigid rotations.
    private const double SpacingTolerance = 0.75;
    private const int Restarts = 4;
    private const int LargeProblemRestarts = 2;
    private const int LargeProblemSize = 600;

    public static Expectations CalculateExpectations(IReadOnlyList<DateOnly> weeks, IReadOnlyList<Participant> participants, int playersPerWeek)
        => new(new Problem(new Input(weeks, participants, playersPerWeek, 0)));

    public Result Schedule(Input input)
    {
        var problem = new Problem(input);

        int[][]? best = null;
        var bestCost = double.MaxValue;
        var restarts = problem.WeekCount * problem.ParticipantCount > LargeProblemSize ? LargeProblemRestarts : Restarts;
        for (var restart = 0; restart < restarts; restart++)
        {
            var random = new Random(unchecked((input.Seed * 397) ^ restart));
            var state = new State(problem);
            state.Construct(random);
            var (players, cost) = state.Anneal(random);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = players;
            }
        }

        var assignments = new Dictionary<DateOnly, IReadOnlyList<int>>();
        for (var w = 0; w < problem.WeekCount; w++)
            assignments[input.Weeks[w]] = (best?[w] ?? []).Order().Select(i => problem.Ids[i]).ToList();

        var understaffed = Enumerable.Range(0, problem.WeekCount)
            .Where(w => problem.Capacity[w] < input.PlayersPerWeek)
            .Select(w => input.Weeks[w])
            .ToList();

        return new Result(assignments, understaffed);
    }

    internal sealed class Problem
    {
        public Problem(Input input)
        {
            Ids = input.Participants.Select(p => p.Id).ToArray();
            ParticipantCount = Ids.Length;
            WeekCount = input.Weeks.Count;

            Available = new bool[WeekCount][];
            Capacity = new int[WeekCount];
            AvailableWeekCount = new int[ParticipantCount];
            for (var w = 0; w < WeekCount; w++)
            {
                Available[w] = input.Participants.Select(p => !p.UnavailableWeeks.Contains(input.Weeks[w])).ToArray();
                Capacity[w] = Math.Min(input.PlayersPerWeek, Available[w].Count(a => a));
                for (var i = 0; i < ParticipantCount; i++)
                    if (Available[w][i])
                        AvailableWeekCount[i]++;
            }

            Targets = CalculateTargets(input.Participants.Select(p => Math.Max(p.Weight, 0)).ToArray());
            ExpectedPairs = CalculateExpectedPairs();
            ExpectedByWeek = CalculateExpectedByWeek();
            Pace = Enumerable.Range(0, ParticipantCount)
                .Select(i => AvailableWeekCount[i] == 0 ? 0 : Targets[i] / AvailableWeekCount[i])
                .ToArray();
            AvailablePrefix = Enumerable.Range(0, ParticipantCount)
                .Select(i =>
                {
                    var prefix = new int[WeekCount + 1];
                    for (var w = 0; w < WeekCount; w++)
                        prefix[w + 1] = prefix[w] + (Available[w][i] ? 1 : 0);
                    return prefix;
                })
                .ToArray();
            SpacingWindow = Pace
                .Select(pace => pace <= 0 ? 0 : Math.Min(Math.Min(WeekCount, MaxSpacingWindow), Math.Max(2, (int)Math.Ceiling(2 / Math.Max(Math.Min(pace, 1 - pace), 0.01)))))
                .ToArray();
        }

        public int[] Ids { get; }
        public int ParticipantCount { get; }
        public int WeekCount { get; }
        public bool[][] Available { get; }
        public int[] Capacity { get; }
        public int[] AvailableWeekCount { get; }
        public double[] Targets { get; }
        public double[,] ExpectedPairs { get; }

        /// <summary>Games a participant should have played up to and including a week if spread evenly over their available weeks.</summary>
        public double[][] ExpectedByWeek { get; }

        /// <summary>Number of available weeks before a week, per participant (length WeekCount + 1).</summary>
        public int[][] AvailablePrefix { get; }

        /// <summary>Games per available week.</summary>
        public double[] Pace { get; }

        /// <summary>
        /// Longest stretch of weeks checked for even spacing (0 = participant is not checked). It covers about two
        /// games for rare players and about two rest weeks for frequent players.
        /// </summary>
        public int[] SpacingWindow { get; }

        private double[][] CalculateExpectedByWeek()
        {
            var expected = new double[ParticipantCount][];
            for (var i = 0; i < ParticipantCount; i++)
            {
                expected[i] = new double[WeekCount];
                var availableSoFar = 0;
                for (var w = 0; w < WeekCount; w++)
                {
                    if (Available[w][i])
                        availableSoFar++;

                    expected[i][w] = AvailableWeekCount[i] == 0 ? 0 : Targets[i] * availableSoFar / AvailableWeekCount[i];
                }
            }

            return expected;
        }

        /// <summary>Splits all slots proportionally to the weights; participants that can't reach their share hand the rest to the others.</summary>
        private double[] CalculateTargets(double[] weights)
        {
            var targets = new double[ParticipantCount];
            var capped = new bool[ParticipantCount];
            double remainingSlots = Capacity.Sum();

            while (true)
            {
                var weightSum = Enumerable.Range(0, ParticipantCount).Where(i => !capped[i]).Sum(i => weights[i]);
                if (weightSum <= 0)
                    break;

                var changed = false;
                for (var i = 0; i < ParticipantCount; i++)
                {
                    if (!capped[i])
                        targets[i] = remainingSlots * weights[i] / weightSum;
                }

                for (var i = 0; i < ParticipantCount; i++)
                {
                    if (capped[i] || targets[i] <= AvailableWeekCount[i])
                        continue;

                    targets[i] = AvailableWeekCount[i];
                    capped[i] = true;
                    remainingSlots -= AvailableWeekCount[i];
                    changed = true;
                }

                if (!changed)
                    break;
            }

            return targets;
        }

        /// <summary>e_ij proportional to t_i * t_j, scaled so that all expectations add up to the number of pairs in the schedule.</summary>
        private double[,] CalculateExpectedPairs()
        {
            var expected = new double[ParticipantCount, ParticipantCount];
            var pairSlots = Capacity.Sum(c => c * (c - 1) / 2.0);
            var sum = Targets.Sum();
            var productSum = (sum * sum - Targets.Sum(t => t * t)) / 2;
            if (productSum <= 0)
                return expected;

            for (var i = 0; i < ParticipantCount; i++)
            {
                for (var j = 0; j < ParticipantCount; j++)
                {
                    if (i != j)
                        expected[i, j] = Targets[i] * Targets[j] * pairSlots / productSum;
                }
            }

            return expected;
        }
    }

    private sealed class State(Problem problem)
    {
        private readonly int[][] players = problem.Capacity.Select(c => new int[c]).ToArray();
        private readonly int[] counts = new int[problem.ParticipantCount];
        private readonly int[,] pairs = new int[problem.ParticipantCount, problem.ParticipantCount];
        private readonly bool[][] plays = Enumerable.Range(0, problem.ParticipantCount).Select(_ => new bool[problem.WeekCount]).ToArray();
        private readonly int[] gamesPrefix = new int[problem.WeekCount + 1];

        /// <summary>Fills the weeks chronologically, preferring participants who are furthest behind their even pace.</summary>
        public void Construct(Random random)
        {

            for (var w = 0; w < problem.WeekCount; w++)
            {
                var chosen = new List<int>();
                for (var slot = 0; slot < problem.Capacity[w]; slot++)
                {
                    var best = -1;
                    var bestScore = double.MinValue;
                    for (var i = 0; i < problem.ParticipantCount; i++)
                    {
                        if (!problem.Available[w][i] || chosen.Contains(i))
                            continue;

                        var behind = problem.ExpectedByWeek[i][w] - counts[i];
                        var pairExcess = chosen.Sum(j => pairs[i, j] - problem.ExpectedPairs[i, j]);
                        var score = behind - 0.05 * pairExcess + random.NextDouble() * 1e-6;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = i;
                        }
                    }

                    foreach (var j in chosen)
                    {
                        pairs[best, j]++;
                        pairs[j, best]++;
                    }

                    counts[best]++;
                    chosen.Add(best);
                    players[w][slot] = best;
                    plays[best][w] = true;
                }
            }
        }

        public (int[][] Players, double Cost) Anneal(Random random)
        {
            var cost = Cost();
            var best = Copy();
            var bestCost = cost;

            var weeks = Enumerable.Range(0, problem.WeekCount).Where(w => problem.Capacity[w] > 0).ToArray();
            if (weeks.Length == 0)
                return (best, bestCost);

            var iterations = Math.Clamp(problem.WeekCount * problem.ParticipantCount * 300, 20_000, 300_000);
            const double startTemperature = 20.0;
            const double endTemperature = 0.02;
            var cooling = Math.Pow(endTemperature / startTemperature, 1.0 / iterations);
            var temperature = startTemperature;

            for (var iteration = 0; iteration < iterations; iteration++, temperature *= cooling)
            {
                double? delta = random.NextDouble() < 0.5
                    ? TryReplaceMove(random, weeks, temperature)
                    : TrySwapMove(random, weeks, temperature);

                if (delta is null)
                    continue;

                cost += delta.Value;
                if (cost < bestCost - 1e-9)
                {
                    bestCost = cost;
                    best = Copy();
                }
            }

            return (best, bestCost);
        }

        /// <summary>Replaces a player of a week with someone available who isn't playing that week.</summary>
        private double? TryReplaceMove(Random random, int[] weeks, double temperature)
        {
            var w = weeks[random.Next(weeks.Length)];
            var slot = random.Next(players[w].Length);
            var current = players[w][slot];
            var candidate = random.Next(problem.ParticipantCount);
            if (!problem.Available[w][candidate] || players[w].Contains(candidate))
                return null;

            var delta = Replace(w, slot, candidate);
            if (Accept(delta, temperature, random))
                return delta;

            Replace(w, slot, current);
            return null;
        }

        /// <summary>Exchanges two players between two weeks; keeps the counts, only changes the pairings.</summary>
        private double? TrySwapMove(Random random, int[] weeks, double temperature)
        {
            if (weeks.Length < 2)
                return null;

            var w1 = weeks[random.Next(weeks.Length)];
            var w2 = weeks[random.Next(weeks.Length)];
            if (w1 == w2)
                return null;

            var s1 = random.Next(players[w1].Length);
            var s2 = random.Next(players[w2].Length);
            var x = players[w1][s1];
            var y = players[w2][s2];
            if (x == y || !problem.Available[w2][x] || !problem.Available[w1][y] || players[w1].Contains(y) || players[w2].Contains(x))
                return null;

            var delta = Replace(w1, s1, y) + Replace(w2, s2, x);
            if (Accept(delta, temperature, random))
                return delta;

            Replace(w2, s2, y);
            Replace(w1, s1, x);
            return null;
        }

        private static bool Accept(double delta, double temperature, Random random)
            => delta <= 0 || random.NextDouble() < Math.Exp(-delta / temperature);

        /// <summary>Puts <paramref name="newPlayer"/> into the slot and returns the resulting cost change.</summary>
        private double Replace(int w, int slot, int newPlayer)
        {
            var oldPlayer = players[w][slot];
            var delta = ChangeCount(oldPlayer, -1) + ChangeCount(newPlayer, +1);

            foreach (var other in players[w])
            {
                if (other == oldPlayer)
                    continue;

                delta += ChangePair(oldPlayer, other, -1) + ChangePair(newPlayer, other, +1);
            }

            delta += SpacingWeight * (SpacingDelta(oldPlayer, w, -1) + SpacingDelta(newPlayer, w, +1));
            players[w][slot] = newPlayer;
            plays[oldPlayer][w] = false;
            plays[newPlayer][w] = true;
            return delta;
        }

        /// <summary>
        /// Change of the spacing cost when participant <paramref name="i"/> starts (+1) or stops (-1) playing in week <paramref name="w"/>.
        /// Only the stretches (2 to <see cref="Problem.SpacingWindow"/> weeks long) containing that week are affected.
        /// </summary>
        private double SpacingDelta(int i, int w, int change)
        {
            var maxLength = problem.SpacingWindow[i];
            if (maxLength == 0)
                return 0;

            // Games per week around w as prefix sums, so every stretch is evaluated in O(1)
            var from = Math.Max(0, w - maxLength + 1);
            var to = Math.Min(problem.WeekCount - 1, w + maxLength - 1);
            gamesPrefix[0] = 0;
            for (var k = from; k <= to; k++)
                gamesPrefix[k - from + 1] = gamesPrefix[k - from] + (plays[i][k] ? 1 : 0);

            var availablePrefix = problem.AvailablePrefix[i];
            var delta = 0.0;
            for (var length = 2; length <= maxLength; length++)
            {
                var firstStart = Math.Max(from, w - length + 1);
                var lastStart = Math.Min(w, problem.WeekCount - length);
                for (var start = firstStart; start <= lastStart; start++)
                {
                    var games = gamesPrefix[start + length - from] - gamesPrefix[start - from];
                    var available = availablePrefix[start + length] - availablePrefix[start];
                    delta += WindowCost(i, games + change, available) - WindowCost(i, games, available);
                }
            }

            return delta;
        }

        private double TotalSpacingCost()
        {
            var cost = 0.0;
            for (var i = 0; i < problem.ParticipantCount; i++)
            {
                for (var length = 2; length <= problem.SpacingWindow[i]; length++)
                {
                    for (var start = 0; start + length <= problem.WeekCount; start++)
                    {
                        int games = 0, available = 0;
                        for (var k = start; k < start + length; k++)
                        {
                            if (plays[i][k]) games++;
                            if (problem.Available[k][i]) available++;
                        }

                        cost += WindowCost(i, games, available);
                    }
                }
            }

            return cost;
        }

        private double WindowCost(int i, int games, int available)
        {
            var fair = problem.Pace[i] * available;
            var violation = Math.Max(0, games - fair - SpacingTolerance) + Math.Max(0, fair - SpacingTolerance - games);
            return violation * violation;
        }

        private double ChangeCount(int i, int change)
        {
            var before = counts[i] - problem.Targets[i];
            counts[i] += change;
            var after = counts[i] - problem.Targets[i];
            return CountWeight * (after * after - before * before);
        }

        private double ChangePair(int i, int j, int change)
        {
            var before = pairs[i, j] - problem.ExpectedPairs[i, j];
            pairs[i, j] += change;
            pairs[j, i] += change;
            var after = pairs[i, j] - problem.ExpectedPairs[i, j];
            return PairWeight * (after * after - before * before);
        }

        private double Cost()
        {
            var cost = SpacingWeight * TotalSpacingCost();
            for (var i = 0; i < problem.ParticipantCount; i++)
            {
                var countDeviation = counts[i] - problem.Targets[i];
                cost += CountWeight * countDeviation * countDeviation;

                for (var j = i + 1; j < problem.ParticipantCount; j++)
                {
                    var pairDeviation = pairs[i, j] - problem.ExpectedPairs[i, j];
                    cost += PairWeight * pairDeviation * pairDeviation;
                }
            }

            return cost;
        }

        private int[][] Copy() => players.Select(p => p.ToArray()).ToArray();
    }
}
