namespace Backgammon.Logic.Tests.Rules
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Naive;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// Checks the step-by-step validation behind Validate, Act and the move helpers (<see cref="StageChecker"/>) against
    /// the naive generator. Every sequence the naive generator can make, every start of one, and random junk must be
    /// judged a legal play, or the start of one, exactly when the naive generator's legal plays say so; and the helpers
    /// must offer exactly the next steps of the legal plays, in canonical order.
    /// </summary>
    public class StageCheckerDifferentialTests
    {
        // Stages with more sequences than this are sampled.
        private const int SampleSize = 1_500;

        public static TheoryData<BackgammonVersion> Versions => new(Enum.GetValues<BackgammonVersion>());

        [Theory]
        [MemberData(nameof(Versions))]
        public void TheCheckerShouldAgreeWithTheNaiveGenerator(BackgammonVersion version)
        {
            var count = TestScale.Pick(200, 20_000);
            var failures = new ConcurrentQueue<string>();
            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (index, state) =>
            {
                if (!failures.IsEmpty)
                {
                    state.Stop();
                    return;
                }

                var (position, seat) = index % 2 == 0
                    ? PositionSources.Reachable(version, 3, index)
                    : PositionSources.RandomValid(version, 4, index);
                var random = new Random(index);
                foreach (var dice in PositionSources.AllRolls.Append(StageDice.Same(random.Next(1, 7), random.Next(1, 4))))
                {
                    var difference = Difference(position, seat, dice, random);
                    if (difference != null)
                    {
                        failures.Enqueue($"#{index} {PositionCode.Format(position)} seat {seat} dice {dice}: {difference}");
                    }
                }
            });

            Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures.Take(5)));
        }

        private static string? Difference(in Position position, int seat, StageDice dice, Random random)
        {
            var diceList = dice.IsDistinct
                ? new[] { (int)dice.High, dice.Low }
                : Enumerable.Repeat((int)dice.High, dice.Count).ToArray();
            var naive = NaiveGenerator.Generate(NaiveBoard.FromCode(PositionCode.Format(position)), seat, diceList);
            var playable = naive.MaxEffective;

            // The legal plays, every start of one, and the next steps after each start.
            var legal = new HashSet<string>();
            var next = new Dictionary<string, SortedSet<(int From, int Die)>>();
            var starts = new Dictionary<string, List<(int From, int Die)>>();
            foreach (var play in naive.Plays)
            {
                legal.Add(Key(play));
                for (var length = 0; length <= play.Count; length++)
                {
                    var start = play.GetRange(0, length);
                    var key = Key(start);
                    starts.TryAdd(key, start);
                    if (!next.TryGetValue(key, out var steps))
                    {
                        next[key] = steps = new SortedSet<(int From, int Die)>(Comparer<(int From, int Die)>.Create(Canonical));
                    }

                    if (length < play.Count)
                    {
                        steps.Add(play[length]);
                    }
                }
            }

            // Every sequence the naive generator made and every start of one, plus junk, sampled when there are many.
            var candidates = new Dictionary<string, List<(int From, int Die)>>();
            foreach (var sequence in Sample(naive.Sequences, random))
            {
                for (var length = 0; length <= sequence.Count; length++)
                {
                    var start = sequence.GetRange(0, length);
                    candidates.TryAdd(Key(start), start);
                }
            }

            for (var i = 0; i < 40; i++)
            {
                var junk = Enumerable.Range(0, random.Next(1, 5)).Select(_ => (random.Next(0, 27), random.Next(0, 8))).ToList();
                candidates.TryAdd(Key(junk), junk);
            }

            foreach (var (key, sequence) in candidates)
            {
                var steps = ToSteps(sequence);
                var isLegal = StageChecker.IsLegal(position, seat, dice, playable, steps, out _, out _);
                if (isLegal != legal.Contains(key))
                {
                    return $"[{key}] as a play: checker {isLegal}, naive {!isLegal}";
                }

                var isStart = StageChecker.IsLegalPrefix(position, seat, dice, playable, steps);
                if (isStart != next.ContainsKey(key))
                {
                    return $"[{key}] as the start of a play: checker {isStart}, naive {!isStart}";
                }
            }

            // The helpers' next steps after each start of a legal play.
            var ends = new List<StageEnd>();
            new StageGenerator().Generate(position, seat, dice, ends);
            var moves = new BackgammonStageMoves(position, seat, dice, playable, ends, diceList);
            foreach (var start in Sample(starts.Values.ToList(), random).Prepend(new List<(int From, int Die)>()))
            {
                var offered = moves.NextSteps(ToSteps(start)).Select(o => (o.Step.From, o.Step.Die)).ToList();
                var expected = next[Key(start)].ToList();
                if (!offered.SequenceEqual(expected))
                {
                    return $"after [{Key(start)}] the helpers offer [{Key(offered)}], the legal plays go on with [{Key(expected)}]";
                }
            }

            return null;
        }

        private static IEnumerable<List<(int From, int Die)>> Sample(IReadOnlyList<List<(int From, int Die)>> all, Random random)
        {
            if (all.Count <= SampleSize)
            {
                return all;
            }

            return Enumerable.Range(0, SampleSize).Select(_ => all[random.Next(all.Count)]);
        }

        private static int Canonical((int From, int Die) a, (int From, int Die) b) =>
            a.From != b.From ? b.From.CompareTo(a.From) : b.Die.CompareTo(a.Die);

        private static BackgammonStep[] ToSteps(List<(int From, int Die)> sequence) =>
            sequence.Select(s => new BackgammonStep(s.From, s.Die)).ToArray();

        private static string Key(IEnumerable<(int From, int Die)> steps) => string.Join(' ', steps.Select(s => $"{s.From}/{s.Die}"));
    }
}
