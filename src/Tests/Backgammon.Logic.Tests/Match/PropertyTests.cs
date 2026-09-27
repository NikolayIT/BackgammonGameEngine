namespace Backgammon.Logic.Tests.Match
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// Random full games of every variant, checking the invariants after every stage:
    /// <list type="bullet">
    /// <item>each player has 15 checkers;</item>
    /// <item>no point is shared except by a тапа pin;</item>
    /// <item>the pip counts agree with the board;</item>
    /// <item>the move helpers agree with Validate and Act;</item>
    /// <item>every game ends and the scores add up;</item>
    /// <item>every die is accounted for by the record.</item>
    /// </list>
    /// There are 2,000 games per variant by default and 100k with BACKGAMMON_LONG=1.
    /// </summary>
    public class PropertyTests
    {
        public static TheoryData<BackgammonVariant> Variants => new(Enum.GetValues<BackgammonVariant>());

        [Theory]
        [MemberData(nameof(Variants))]
        public void RandomGamesShouldKeepEveryInvariant(BackgammonVariant variant)
        {
            var wanted = TestScale.Pick(2_000, 100_000);
            var failures = new ConcurrentQueue<string>();
            var games = 0L;
            var matches = 0;
            Parallel.For(0, int.MaxValue, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (seed, state) =>
            {
                if (Interlocked.Read(ref games) >= wanted || !failures.IsEmpty)
                {
                    state.Stop();
                    return;
                }

                try
                {
                    Interlocked.Add(ref games, PlayChecked(variant, seed));
                    Interlocked.Increment(ref matches);
                }
                catch (Exception exception)
                {
                    failures.Enqueue($"{variant} seed {seed}: {exception.Message}");
                    state.Stop();
                }
            });

            Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures.Take(3)));
            Assert.True(games >= wanted);
        }

        internal static void CheckBoard(BackgammonSeatView view)
        {
            var board = view.Board;
            var version = view.Version;
            var totals = new[] { board.Bar[0] + board.Off[0], board.Bar[1] + board.Off[1] };
            var pips = new[] { 25 * board.Bar[0], 25 * board.Bar[1] };
            if (version is BackgammonVersion.Gyulbara or BackgammonVersion.Tapa)
            {
                Check(board.Bar[0] == 0 && board.Bar[1] == 0, "a checker on the bar in a version without one");
            }

            for (var index = 0; index < 24; index++)
            {
                var point = board.Points[index];
                Check(point.Number == index + 1, "points out of order");
                totals[0] += point.Seat0;
                totals[1] += point.Seat1;
                pips[0] += point.Number * point.Seat0;
                pips[1] += BackgammonGeometry.FromSeat0(version, 1, point.Number) * point.Seat1;
                if (point.Seat0 > 0 && point.Seat1 > 0)
                {
                    Check(version == BackgammonVersion.Tapa && point.PinnedSeat >= 0, $"point {point.Number} is shared without a pin");
                }

                if (point.PinnedSeat >= 0)
                {
                    var pinned = point.PinnedSeat == 0 ? point.Seat0 : point.Seat1;
                    var pinners = point.PinnedSeat == 0 ? point.Seat1 : point.Seat0;
                    Check(version == BackgammonVersion.Tapa && pinned == 1 && pinners >= 1, $"point {point.Number} has a broken pin");
                }
            }

            Check(totals[0] == 15 && totals[1] == 15, $"checker counts {totals[0]} and {totals[1]}");
            Check(pips[0] == view.Pips[0] && pips[1] == view.Pips[1], $"pips {view.Pips[0]}/{view.Pips[1]}, board says {pips[0]}/{pips[1]}");
        }

        private static int PlayChecked(BackgammonVariant variant, int seed)
        {
            var dice = LoggedDice.Seeded(seed);
            var match = new BackgammonMatch(new BackgammonMatchOptions
            {
                Variant = variant,
                TargetPoints = 1 + (seed % 2),
                Dice = dice.Source,
                AutoPlayForcedStages = seed % 3 == 0,
            });
            var random = new Random(seed);
            match.Start();
            var decisions = 0;
            while (!match.IsFinished)
            {
                Check(decisions++ < 20_000, "the match does not end");
                var seat = match.ToMove;
                var view = match.GetView(seat);
                CheckBoard(view);
                Check(view.StuckRolls < BackgammonMatch.StuckRollLimit, "the stuck count passed its limit");
                Check(view.StageDice.Count >= 1 && view.StageDice.Count <= 4, "a stage without dice");

                var moves = match.GetStageMoves();
                Check(moves.PlayableDice > 0 && moves.OutcomeCount >= 1, "a decision with nothing to play");
                Check(moves.PlayableDice <= view.StageDice.Count, "more dice to play than the stage has");
                var sample = moves.Outcomes[random.Next(moves.OutcomeCount)];
                Check(moves.IsLegal(sample.Steps), "an outcome's canonical steps are not legal");

                if (decisions % 7 == 0)
                {
                    var fromView = BackgammonStageMoves.For(Json.Deserialize(Json.Serialize(view)));
                    Check(fromView.OutcomeCount == moves.OutcomeCount && fromView.PlayableDice == moves.PlayableDice, "the view's moves differ from the match's");
                }

                var action = MatchDriver.RandomAction(match, random);
                Check(match.Validate(seat, action) == BackgammonActResult.Ok, $"a helper-built action is refused: {action}");
                Check(match.Act(seat, action) == BackgammonActResult.Ok, "Act refuses what Validate accepts");
            }

            var final = match.GetFinalView();
            CheckBoard(final);
            var results = final.Results;
            Check(results.Sum(r => r.Points[0]) == final.Scores[0] && results.Sum(r => r.Points[1]) == final.Scores[1], "the scores do not add up");
            Check(final.Scores.Max() >= final.TargetPoints && final.Scores[0] != final.Scores[1], "the match ended without a leader at the target");
            Check(final.MatchWinner == (final.Scores[0] > final.Scores[1] ? 0 : 1), "the wrong winner");
            foreach (var result in results)
            {
                var expected = result.Kind switch
                {
                    BackgammonResultKind.Normal => 1,
                    BackgammonResultKind.Mars or BackgammonResultKind.Mother => 2,
                    _ => 0,
                };

                var scoredRight = result.Kind is BackgammonResultKind.Draw or BackgammonResultKind.Stuck
                    ? result.Winner == -1 && result.Points[0] == result.Points[1]
                    : result.Points[result.Winner] == expected && result.Points[1 - result.Winner] == 0;
                Check(scoredRight, $"game {result.GameNumber} scored wrongly");
                Check(result.Version == BackgammonMatch.VersionOf(variant, result.GameNumber), "a game played by the wrong version");
                Check(result.Kind != BackgammonResultKind.Mother || result.Version == BackgammonVersion.Tapa, "a майка outside тапа");
            }

            Check(dice.Log.Select(d => (d.Purpose, d.Die)).SequenceEqual(LoggedDice.DrawsOf(final.Record!)), "the draws do not match the record");
            return results.Count;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
