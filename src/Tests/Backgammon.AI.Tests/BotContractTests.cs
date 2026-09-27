namespace Backgammon.AI.Tests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;

    using Backgammon.Logic;
    using Xunit;

    /// <summary>
    /// What a host relies on:
    /// <list type="bullet">
    /// <item>a bot plays only legal actions, in every variant and situation (chains, remainders, forced stages);</item>
    /// <item>it decides from the view alone, the same for the same view, level and Random;</item>
    /// <item>it is fast.</item>
    /// </list>
    /// </summary>
    public class BotContractTests
    {
        public static TheoryData<BackgammonVariant> Variants => new(Enum.GetValues<BackgammonVariant>());

        [Theory]
        [MemberData(nameof(Variants))]
        public void EveryLevelShouldOnlyPlayLegalActions(BackgammonVariant variant)
        {
            for (var level = 1; level <= BackgammonBot.Levels; level++)
            {
                var dice = new Random(level);
                var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = (n, _) => dice.Next(n) });
                var random = new Random(level * 7);
                match.Start();
                while (!match.IsFinished)
                {
                    var seat = match.ToMove;
                    var action = BackgammonBot.Choose(match.GetView(seat), level, random);
                    Assert.Equal(BackgammonActResult.Ok, match.Act(seat, action));
                }
            }
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void ABotShouldDecideTheSameFromTheSameViewLevelAndRandom(BackgammonVariant variant)
        {
            var dice = new Random(3);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = (n, _) => dice.Next(n) });
            match.Start();
            var decisions = 0;
            while (!match.IsFinished && decisions++ < 150)
            {
                var seat = match.ToMove;
                var view = match.GetView(seat);
                var level = 1 + (decisions % BackgammonBot.Levels);
                var first = BackgammonBot.Decide(view, level, new Random(decisions));
                var again = BackgammonBot.Decide(view, level, new Random(decisions));

                // From the view read back from host JSON, and from a view without any history, too.
                var wire = JsonSerializer.Deserialize(JsonSerializer.Serialize(view, HostJsonContext.Default.BackgammonSeatView), HostJsonContext.Default.BackgammonSeatView)!;
                var bare = new BackgammonSeatView
                {
                    Seat = view.Seat,
                    Variant = view.Variant,
                    Version = view.Version,
                    GameNumber = view.GameNumber,
                    TargetPoints = view.TargetPoints,
                    Scores = view.Scores,
                    ToMove = view.ToMove,
                    Board = view.Board,
                    StageDice = view.StageDice,
                    ChainRest = view.ChainRest,
                    IsEscalating = view.IsEscalating,
                    IsPlayingRemainder = view.IsPlayingRemainder,
                    RollsMade = view.RollsMade,
                };

                Assert.Equal(first.Action.Steps, again.Action.Steps);
                Assert.Equal(first.Action.Steps, BackgammonBot.Decide(wire, level, new Random(decisions)).Action.Steps);
                Assert.Equal(first.Action.Steps, BackgammonBot.Decide(bare, level, new Random(decisions)).Action.Steps);
                Assert.Equal(first.Complexity, BackgammonBot.Complexity(view));
                Assert.Equal(BackgammonActResult.Ok, match.Act(seat, first.Action));
            }
        }

        [Fact]
        public void TheComplexityShouldBeZeroForAForcedPlayAndGrowWithTheChoices()
        {
            Assert.Equal(0, BackgammonBot.ComplexityOf(1));
            Assert.Equal(1, BackgammonBot.ComplexityOf(16), 6);
            Assert.True(BackgammonBot.ComplexityOf(100) > 1.5);
            Assert.Equal(3, BackgammonBot.ComplexityOf(100_000));
        }

        [Fact]
        public void ABadLevelOrAViewWithNobodyToMoveShouldBeRefused()
        {
            var match = new BackgammonMatch();
            match.Start();
            var view = match.GetView(match.ToMove);

            Assert.Throws<ArgumentOutOfRangeException>(() => BackgammonBot.Choose(view, 0, new Random(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BackgammonBot.Choose(view, 7, new Random(1)));
            match.Stop();
            Assert.Throws<ArgumentException>(() => BackgammonBot.Choose(match.GetView(0), 6, new Random(1)));

            // A view of an unknown version used to fail with IndexOutOfRangeException.
            var unknown = new BackgammonSeatView { Version = (BackgammonVersion)9, ToMove = 0, Board = view.Board, StageDice = view.StageDice };
            Assert.Throws<ArgumentException>(() => BackgammonBot.Choose(unknown, 6, new Random(1)));
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void TheStrongestLevelShouldDecideQuickly(BackgammonVariant variant)
        {
            var dice = new Random(9);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = (n, _) => dice.Next(n) });
            var random = new Random(9);
            match.Start();
            BackgammonBot.Choose(match.GetView(match.ToMove), BackgammonBot.Levels, random);
            var clock = Stopwatch.StartNew();
            var decisions = 0;
            while (!match.IsFinished)
            {
                var seat = match.ToMove;
                match.Act(seat, BackgammonBot.Choose(match.GetView(seat), BackgammonBot.Levels, random));
                decisions++;
            }

            // A generous bound for a shared CI machine; the arena measures the real percentiles on one core.
            Assert.True(clock.Elapsed.TotalMilliseconds / decisions < 20, $"{clock.Elapsed.TotalMilliseconds / decisions:F1} ms a decision");
        }
    }
}
