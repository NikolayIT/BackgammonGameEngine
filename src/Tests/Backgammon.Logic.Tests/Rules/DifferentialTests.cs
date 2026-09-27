namespace Backgammon.Logic.Tests.Rules
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Naive;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// Checks the fast stage generator against the naive brute-force one on many positions, for all 21 rolls and
    /// for partial stages of equal dice (the rest of an escalating chain). The two must agree on how many dice are
    /// played, on the set of distinct end positions, on the canonical steps to each end, and on how each end
    /// finishes the game.
    /// </summary>
    public class DifferentialTests
    {
        public static TheoryData<BackgammonVersion> Versions => new(Enum.GetValues<BackgammonVersion>());

        [Theory]
        [MemberData(nameof(Versions))]
        public void TheFastGeneratorShouldAgreeWithTheNaiveOneOnReachablePositions(BackgammonVersion version)
        {
            Compare(version, TestScale.Pick(600, 60_000), index => PositionSources.Reachable(version, 1, index));
        }

        [Theory]
        [MemberData(nameof(Versions))]
        public void TheFastGeneratorShouldAgreeWithTheNaiveOneOnRandomValidPositions(BackgammonVersion version)
        {
            Compare(version, TestScale.Pick(400, 40_000), index => PositionSources.RandomValid(version, 2, index));
        }

        [Theory]
        [MemberData(nameof(Versions))]
        public void ThePositionsToCompareOnShouldBeValid(BackgammonVersion version)
        {
            // Reading a position back through a view board checks it: 15 checkers a seat, pins and the bar allowed.
            for (var index = 0; index < 3_000; index++)
            {
                var (position, _) = index % 10 == 0 ? PositionSources.Reachable(version, 1, index) : PositionSources.RandomValid(version, 2, index);

                Assert.Equal(position, ViewConverter.ToPosition(version, ViewConverter.ToBoard(position)));
            }
        }

        internal static string? Difference(StageGenerator generator, List<StageEnd> ends, in Position position, int seat, StageDice dice)
        {
            var max = generator.Generate(position, seat, dice, ends);
            var diceList = dice.IsDistinct
                ? new[] { (int)dice.High, dice.Low }
                : Enumerable.Repeat((int)dice.High, dice.Count).ToArray();
            var naive = NaiveGenerator.Generate(NaiveBoard.FromCode(PositionCode.Format(position)), seat, diceList);

            var where = $"{PositionCode.Format(position)} seat {seat} dice {dice}";
            if (max != naive.MaxEffective)
            {
                return $"{where}: fast plays {max} dice, naive {naive.MaxEffective}";
            }

            if (ends.Count != naive.Ends.Count)
            {
                var fast = ends.Select(e => PositionCode.Format(e.Position)).ToHashSet();
                var missing = naive.Ends.Keys.Where(k => !fast.Contains(k)).Take(3);
                var extra = fast.Where(k => !naive.Ends.ContainsKey(k)).Take(3);
                return $"{where}: fast {ends.Count} ends, naive {naive.Ends.Count}; missing [{string.Join("; ", missing)}] extra [{string.Join("; ", extra)}]";
            }

            for (var i = 0; i < ends.Count; i++)
            {
                var code = PositionCode.Format(ends[i].Position);
                if (!naive.Ends.TryGetValue(code, out var expected))
                {
                    return $"{where}: fast end {code} is not legal for the naive generator";
                }

                var steps = Enumerable.Range(0, ends[i].Steps.Count).Select(k => (ends[i].Steps.From(k), ends[i].Steps.Die(k))).ToList();
                if (NaiveGenerator.Compare(steps, expected.Steps) != 0)
                {
                    return $"{where}: end {code} canonical steps fast [{ends[i].Steps}] naive [{string.Join(' ', expected.Steps.Select(s => $"{s.From}/{s.Die}"))}]";
                }

                if ((int)ends[i].End != expected.End)
                {
                    return $"{where}: end {code} game end fast {ends[i].End} naive {expected.End}";
                }

                if (i > 0 && ends[i - 1].Steps.CompareTo(ends[i].Steps) >= 0)
                {
                    return $"{where}: ends are not in canonical order at {i}";
                }
            }

            return null;
        }

        private static void Compare(BackgammonVersion version, int count, Func<int, (Position Position, int Seat)> source)
        {
            var failures = new ConcurrentQueue<string>();
            var checkedStages = 0L;
            using var generators = new ThreadLocal<(StageGenerator Generator, List<StageEnd> Ends)>(() => (new StageGenerator(), new List<StageEnd>()));
            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (index, state) =>
            {
                if (failures.Count > 5)
                {
                    state.Stop();
                    return;
                }

                var (position, seat) = source(index);
                var (generator, ends) = generators.Value;
                var random = new Random(index);
                foreach (var roll in PositionSources.AllRolls)
                {
                    Check(roll);
                }

                // The rest of an escalating chain: one to three equal dice.
                Check(StageDice.Same(random.Next(1, 7), random.Next(1, 4)));

                void Check(StageDice dice)
                {
                    Interlocked.Increment(ref checkedStages);
                    var difference = Difference(generator, ends, position, seat, dice);
                    if (difference != null)
                    {
                        failures.Enqueue($"#{index} {difference}");
                    }
                }
            });

            Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures.Take(5)));
            Assert.True(checkedStages >= count * 22L);
        }
    }
}
