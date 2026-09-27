namespace Backgammon.Logic.Tests.Match
{
    using System;
    using System.Linq;

    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// How games are scored (a win 1, марс 2, майка 2, the double-mother draw 1:1, a stuck game 0:0), and how a match
    /// ends: at the target, and only with a lead.
    /// </summary>
    public class ScoringTests
    {
        [Fact]
        public void BearingOffLastWinsOnePointWhenTheLoserHasBorneOffAny()
        {
            var position = TestPosition.Of(BackgammonVersion.Obiknovena).With(0, 1, 1).With(1, 13, 5).Build();
            var match = Started(BackgammonVariant.Obiknovena, position, 0, 2, 1, LoggedDice.Seeded(1));

            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((1, 1))));

            var result = Assert.Single(match.GetView(0).Results);
            Assert.Equal((0, BackgammonResultKind.Normal), (result.Winner, result.Kind));
            Assert.Equal(new[] { 1, 0 }, result.Points);
            Assert.Equal(new[] { 1, 0 }, match.Scores);
            Assert.Equal(2, match.GameNumber);
        }

        [Fact]
        public void BearingOffLastIsMarsWhenTheLoserHasBorneOffNone()
        {
            var position = TestPosition.Of(BackgammonVersion.Obiknovena).With(0, 1, 1).With(1, 13, 15).Build();
            var match = Started(BackgammonVariant.Obiknovena, position, 0, 2, 1, LoggedDice.Seeded(1));

            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((1, 2))));

            var result = Assert.Single(match.GetView(0).Results);
            Assert.Equal((0, BackgammonResultKind.Mars), (result.Winner, result.Kind));
            Assert.Equal(new[] { 2, 0 }, match.Scores);
        }

        [Fact]
        public void TheRestOfTheRollIsNotPlayedAfterTheLastCheckerIsOff()
        {
            var position = TestPosition.Of(BackgammonVersion.Obiknovena).With(0, 1, 1).With(1, 13, 5).Build();
            var match = Started(BackgammonVariant.Obiknovena, position, 0, 2, 1, LoggedDice.Seeded(1));

            Assert.Equal(BackgammonActResult.Illegal, match.Validate(0, MatchDriver.Steps((1, 1), (1, 2))));
            Assert.Equal(BackgammonActResult.Ok, match.Validate(0, MatchDriver.Steps((1, 2))));
            Assert.Equal(BackgammonActResult.Ok, match.Validate(0, MatchDriver.Steps((1, 1))));
        }

        [Fact]
        public void PinningTheMotherWinsTwoPointsAtOnce()
        {
            var position = TestPosition.Of(BackgammonVersion.Tapa).With(0, 3, 1).With(0, 13, 5).With(1, 24, 1).With(1, 10, 5).Build();
            var match = Started(BackgammonVariant.Tapa, position, 0, 2, 1, LoggedDice.Seeded(1));

            // Winning is not forced: any play of both dice is legal too.
            Assert.Equal(BackgammonActResult.Ok, match.Validate(0, MatchDriver.Steps((13, 2), (11, 1))));
            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((3, 2))));

            var result = Assert.Single(match.GetView(0).Results);
            Assert.Equal((0, BackgammonResultKind.Mother), (result.Winner, result.Kind));
            Assert.Equal(new[] { 2, 0 }, match.Scores);
        }

        [Fact]
        public void BothMothersPinnedIsADrawOfOnePointEach()
        {
            var position = TestPosition.Of(BackgammonVersion.Tapa).Pinned(1, 24).With(0, 24, 1).With(0, 13, 5).With(1, 3, 1).With(1, 10, 5).Build();
            var match = Started(BackgammonVariant.Tapa, position, 1, 2, 1, LoggedDice.Seeded(1));

            Assert.Equal(BackgammonActResult.Ok, match.Act(1, MatchDriver.Steps((3, 2))));

            var result = Assert.Single(match.GetView(0).Results);
            Assert.Equal((-1, BackgammonResultKind.Draw), (result.Winner, result.Kind));
            Assert.Equal(new[] { 1, 1 }, match.Scores);
        }

        [Fact]
        public void AGameInWhichNobodyCanMoveForAHundredRollsEndsWithNoPoints()
        {
            // Both sides have a checker on the bar against a closed board.
            var frozen = TestPosition.Of(BackgammonVersion.Obiknovena).With(0, Geometry.Bar, 1).With(1, Geometry.Bar, 1).With(0, 13, 2).With(1, 13, 2);
            for (var point = 1; point <= 6; point++)
            {
                frozen.With(0, point, 2).With(1, point, 2);
            }

            var dice = LoggedDice.Seeded(7);
            var match = Started(BackgammonVariant.Obiknovena, frozen.Build(), 0, 3, 1, dice);

            var result = Assert.Single(match.GetView(0).Results);
            Assert.Equal((-1, BackgammonResultKind.Stuck), (result.Winner, result.Kind));
            Assert.Equal(new[] { 0, 0 }, match.Scores);
            Assert.Equal(2, match.GameNumber);

            // The first roll was given; 99 more were drawn, then the next game's opening.
            Assert.Equal(198, dice.Log.TakeWhile(d => d.Purpose == BackgammonMatchOptions.DicePurpose).Count());
            Assert.Equal(BackgammonMatchOptions.OpeningPurpose, dice.Log[198].Purpose);
            var passes = match.GetFinalView().Record!.Games[0].Plays;
            Assert.Equal(BackgammonMatch.StuckRollLimit, passes.Count);
            Assert.All(passes, play => Assert.True(play.Auto && play.Steps.Count == 0));
        }

        [Fact]
        public void AMatchEndsWhenAPlayerReachesTheTargetWithALead()
        {
            var position = TestPosition.Of(BackgammonVersion.Obiknovena).With(0, 1, 1).With(1, 13, 5).Build();
            var match = Started(BackgammonVariant.Obiknovena, position, 0, 2, 1, LoggedDice.Seeded(1), scores: new[] { 2, 1 });

            match.Act(0, MatchDriver.Steps((1, 2)));

            Assert.True(match.IsFinished);
            Assert.False(match.IsStopped);
            Assert.Equal(0, match.Winner);
            Assert.Equal(-1, match.ToMove);
            Assert.Equal(new[] { 3, 1 }, match.Scores);
            Assert.Equal(BackgammonActResult.MatchFinished, match.Act(0, MatchDriver.Steps((1, 2))));
        }

        [Fact]
        public void ADrawThatReachesTheTargetWithoutALeadDoesNotEndTheMatch()
        {
            var position = TestPosition.Of(BackgammonVersion.Tapa).Pinned(1, 24).With(0, 24, 1).With(0, 13, 5).With(1, 3, 1).With(1, 10, 5).Build();
            var match = Started(BackgammonVariant.Tapa, position, 1, 2, 1, LoggedDice.Seeded(5), scores: new[] { 2, 2 });

            match.Act(1, MatchDriver.Steps((3, 2)));

            Assert.False(match.IsFinished);
            Assert.Equal(new[] { 3, 3 }, match.Scores);
            Assert.Equal(2, match.GameNumber);

            MatchDriver.PlayOut(match, new Random(5));
            Assert.True(match.Scores.Max() >= 3);
            Assert.NotEqual(match.Scores[0], match.Scores[1]);
            Assert.Equal(match.Scores[0] > match.Scores[1] ? 0 : 1, match.Winner);
        }

        [Fact]
        public void ADrawCanEndTheMatchWhenTheLeaderReachesTheTarget()
        {
            var position = TestPosition.Of(BackgammonVersion.Tapa).Pinned(1, 24).With(0, 24, 1).With(0, 13, 5).With(1, 3, 1).With(1, 10, 5).Build();
            var match = Started(BackgammonVariant.Sreshta, position, 1, 2, 1, LoggedDice.Seeded(1), scores: new[] { 2, 4 });

            match.Act(1, MatchDriver.Steps((3, 2)));

            Assert.True(match.IsFinished);
            Assert.Equal(1, match.Winner);
            Assert.Equal(new[] { 3, 5 }, match.Scores);
        }

        [Fact]
        public void ASreshtaRotatesObiknovenaGyulbaraAndTapa()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Sreshta, Dice = LoggedDice.Seeded(seed).Source });
                match.Start();
                MatchDriver.PlayOut(match, new Random(seed));

                var results = match.GetView(0).Results;
                Assert.True(results.Count >= 3);
                for (var index = 0; index < results.Count; index++)
                {
                    Assert.Equal(index + 1, results[index].GameNumber);
                    Assert.Equal((BackgammonVersion)(index % 3), results[index].Version);
                }

                Assert.Equal(5, match.TargetPoints);
                Assert.True(match.Scores.Max() >= 5);
            }
        }

        [Fact]
        public void TheDefaultTargetsAreThreeAndFiveForASreshta()
        {
            Assert.Equal(3, new BackgammonMatch().TargetPoints);
            Assert.Equal(3, new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Tapa }).TargetPoints);
            Assert.Equal(5, new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Sreshta }).TargetPoints);
            Assert.Equal(7, new BackgammonMatch(new BackgammonMatchOptions { TargetPoints = 7 }).TargetPoints);
            Assert.Throws<ArgumentOutOfRangeException>(() => new BackgammonMatch(new BackgammonMatchOptions { TargetPoints = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BackgammonMatch(new BackgammonMatchOptions { Variant = (BackgammonVariant)9 }));
        }

        private static BackgammonMatch Started(BackgammonVariant variant, in Position position, int seat, int first, int second, LoggedDice dice, int[]? scores = null)
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = dice.Source });
            match.StartAt(position, seat, first, second, new[] { 5, 5 }, scores);
            return match;
        }
    }
}
