namespace Backgammon.AI.Tests
{
    using System;
    using System.Collections.Generic;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Search;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;
    using Xunit;

    /// <summary>What the search asks its evaluator: which seat is about to roll after each kind of stage.</summary>
    public class SearchTests
    {
        [Theory]
        [InlineData(0)] // levels 1..5: the plays valued as if the turn ended with the stage
        [InlineData(3)] // level 6: the best plays played on through the rest of the remainder
        public void EveryStageOfARemainderShouldBeValuedWithItsPlayerToRollNext(int lookAhead)
        {
            // Seat 0 plays the 2x2 rest of seat 1's chain, with four 3s, 4s, 5s and 6s to come. Seat 0 rolls once it
            // has played them; seat 1 does not roll before that. Valuing a play here with seat 1 to roll picked a
            // different play in about half of such decisions.
            var position = PositionCode.Parse("C:,-1,2,3,3,2,3,,1,,,,,,,,,,1,,,,-2,-7|0,0|0,5");
            var rest = new List<StageDice> { StageDice.Same(3, 4), StageDice.Same(4, 4), StageDice.Same(5, 4), StageDice.Same(6, 4) };
            var situation = new Situation(position, 0, StageDice.Same(2, 2), rest, isRemainder: true, isEscalating: true, new RollCounts(5, 5), BackgammonVariant.Chelebi, 3, 0, 0, 1);
            var spy = new SpyEvaluator();

            new Chooser().Choose(situation, spy, new SearchSettings(0.01, lookAhead, 3_000, 2), new Random(1));

            Assert.NotEmpty(spy.OnRoll);
            Assert.All(spy.OnRoll, seat => Assert.Equal(0, seat));
        }

        [Fact]
        public void TheStagesOfTheMoversOwnChainShouldBeValuedWithTheOpponentToRollNext()
        {
            // Seat 0 plays its own escalating 5-5: four 5s, then four 6s. After its chain seat 1 rolls.
            var position = PositionCode.Parse("C:,-1,2,3,3,2,3,,1,,,,,,,,,,1,,,,-2,-7|0,0|0,5");
            var rest = new List<StageDice> { StageDice.Same(6, 4) };
            var situation = new Situation(position, 0, StageDice.Same(5, 4), rest, isRemainder: false, isEscalating: true, new RollCounts(5, 5), BackgammonVariant.Chelebi, 3, 0, 0, 1);
            var spy = new SpyEvaluator();

            new Chooser().Choose(situation, spy, new SearchSettings(0.01, 0, 0), new Random(1));

            Assert.NotEmpty(spy.OnRoll);
            Assert.All(spy.OnRoll, seat => Assert.Equal(1, seat));
        }

        [Fact]
        public void TheSeatToRollAfterAStageShouldBeTheSameForTheBotsAndTheTrainer()
        {
            // The trainer's self-play picks its plays with this rule too.
            Assert.Equal(0, Chooser.OnRollAfter(isRemainder: true, 0));
            Assert.Equal(1, Chooser.OnRollAfter(isRemainder: true, 1));
            Assert.Equal(1, Chooser.OnRollAfter(isRemainder: false, 0));
            Assert.Equal(0, Chooser.OnRollAfter(isRemainder: false, 1));
        }

        private sealed class SpyEvaluator : IEvaluator
        {
            public List<int> OnRoll { get; } = new();

            public Outcome Evaluate(in Position position, int onRoll, RollCounts rollsMade)
            {
                this.OnRoll.Add(onRoll);
                return BaselineEvaluator.Instance.Evaluate(position, onRoll, rollsMade);
            }
        }
    }
}
