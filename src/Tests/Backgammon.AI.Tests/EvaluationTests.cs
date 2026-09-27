namespace Backgammon.AI.Tests
{
    using System;

    using Backgammon.AI.Evaluation;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;
    using Xunit;

    /// <summary>The match equity table and the baseline evaluator behave sensibly on plain cases.</summary>
    public class EvaluationTests
    {
        [Theory]
        [InlineData(BackgammonVariant.Obiknovena, 3)]
        [InlineData(BackgammonVariant.Tapa, 3)]
        [InlineData(BackgammonVariant.Sreshta, 5)]
        public void MatchEquityShouldBeSymmetricMonotonicAndExactAtTheEnds(BackgammonVariant variant, int target)
        {
            var table = MatchEquity.For(variant, target);
            for (var game = 1; game <= 4; game++)
            {
                for (var a = 0; a <= target + 1; a++)
                {
                    for (var b = 0; b <= target + 1; b++)
                    {
                        Assert.Equal(1, table.Get(a, b, game) + table.Get(b, a, game), 9);
                        if (a + 1 <= target + 1)
                        {
                            Assert.True(table.Get(a + 1, b, game) >= table.Get(a, b, game) - 1e-12);
                        }
                    }
                }

                Assert.Equal(1, table.Get(target, target - 1, game));
                Assert.Equal(0.5, table.Get(target, target, game));
                Assert.Equal(0.5, table.Get(0, 0, game));
            }
        }

        [Fact]
        public void AMarsShouldCountOnlyWhenItChangesTheMatch()
        {
            var table = MatchEquity.For(BackgammonVariant.Obiknovena, 3);

            // At 2:2 any win takes the match; at 0:0 a марс is worth more than a single win.
            var even = table.AfterGame(2, 2, 5);
            Assert.Equal(even.WinSingle, even.WinDouble);
            var start = table.AfterGame(0, 0, 1);
            Assert.True(start.WinDouble > start.WinSingle);
        }

        [Fact]
        public void AWonRaceShouldBeAlmostCertain()
        {
            // Seat 0 is nearly off, seat 1 has everything far away: no contact, a huge lead.
            var position = new Position { Version = BackgammonVersion.Obiknovena };
            position.Set(0, 2, 3);
            position.Set(0, Geometry.Off, 12);
            position.Set(1, 20, 15);

            var outcome = BaselineEvaluator.Instance.Evaluate(position, 0, new RollCounts(10, 10));

            Assert.True(outcome.Win > 0.97, outcome.ToString());
            Assert.True(outcome.WinDouble > 0.9, outcome.ToString());
            Assert.Equal(1, outcome.WinSingle + outcome.WinDouble + outcome.LoseSingle + outcome.LoseDouble + outcome.Draw, 9);
            var flipped = BaselineEvaluator.Instance.Evaluate(position, 1, new RollCounts(10, 10));
            Assert.True(flipped.Win < 0.03, flipped.ToString());
        }

        [Theory]
        [InlineData(BackgammonVersion.Obiknovena)]
        [InlineData(BackgammonVersion.Gyulbara)]
        [InlineData(BackgammonVersion.Tapa)]
        [InlineData(BackgammonVersion.Chelebi)]
        public void TheStartShouldBeCloseToEvenWithASmallEdgeOnRoll(BackgammonVersion version)
        {
            var outcome = BaselineEvaluator.Instance.Evaluate(Position.Start(version), 0, new RollCounts(0, 1));

            Assert.InRange(outcome.Win, 0.45, 0.65);
        }
    }
}
