namespace Backgammon.Logic.Tests.Rules
{
    using System.Collections.Generic;
    using System.Linq;

    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// The move rules shared by every version, shown on обикновена: whole rolls, the larger die, doubles, bearing off,
    /// the bar and hitting. Unplaced checkers count as borne off (see <see cref="TestPosition"/>).
    /// </summary>
    public class MoveRulesTests
    {
        private const BackgammonVersion Obiknovena = BackgammonVersion.Obiknovena;

        [Fact]
        public void BothDiceMustBePlayedWhenTheyCanBe()
        {
            // 13/6 is blocked (seat 1 holds seat 0's 7), but 13/5 then 8/6 plays both dice.
            var position = TestPosition.Of(Obiknovena).With(0, 13, 1).With(1, 18, 2).Build();

            var (max, ends) = Generate(position, 0, StageDice.Distinct(6, 5));

            Assert.Equal(2, max);
            var end = Assert.Single(ends);
            Assert.Equal("13/5 8/6", end.Steps.ToString());
            Assert.Equal(1, end.Position.Count(0, 2));
        }

        [Fact]
        public void WhenOnlyOneDieCanBePlayedItMustBeTheLarger()
        {
            // Either die alone can be played, but not both (seat 1 holds seat 0's 2): the 6 must be played.
            var position = TestPosition.Of(Obiknovena).With(0, 13, 1).With(1, 23, 2).Build();

            var (max, ends) = Generate(position, 0, StageDice.Distinct(6, 5));

            Assert.Equal(1, max);
            Assert.Equal("13/6", Assert.Single(ends).Steps.ToString());
        }

        [Fact]
        public void WhenOnlyTheSmallerDieCanBePlayedItIsPlayed()
        {
            var position = TestPosition.Of(Obiknovena).With(0, 13, 1).With(1, 18, 2).With(1, 23, 2).Build();

            var (max, ends) = Generate(position, 0, StageDice.Distinct(6, 5));

            Assert.Equal(1, max);
            Assert.Equal("13/5", Assert.Single(ends).Steps.ToString());
        }

        [Fact]
        public void ADoubleIsPlayedFourTimes()
        {
            var position = TestPosition.Of(Obiknovena).With(0, 13, 2).With(0, 24, 2).Build();

            var (max, ends) = Generate(position, 0, StageDice.Same(2, 4));

            Assert.Equal(4, max);
            Assert.All(ends, end => Assert.Equal(4, end.Steps.Count));
            Assert.Equal(ends.Count, ends.Select(e => e.Position).Distinct().Count());
        }

        [Fact]
        public void AsManyDiceOfADoubleAsPossibleArePlayed()
        {
            // 13/4 to 9, then 9/4 is blocked by seat 1 on seat 0's 5.
            var position = TestPosition.Of(Obiknovena).With(0, 13, 1).With(1, 20, 2).Build();

            var (max, ends) = Generate(position, 0, StageDice.Same(4, 4));

            Assert.Equal(1, max);
            Assert.Equal("13/4", Assert.Single(ends).Steps.ToString());
        }

        [Fact]
        public void BearingOffUsesTheExactDieOrAHigherOneFromTheHighestPoint()
        {
            var position = TestPosition.Of(Obiknovena).With(0, 6, 1).With(0, 3, 1).With(0, 2, 1).With(1, 20, 12).Build();

            Assert.True(StepRules.CanStep(position, 0, 6, 6));
            Assert.True(StepRules.CanStep(position, 0, 3, 3));
            Assert.False(StepRules.CanStep(position, 0, 2, 3));

            var high = TestPosition.Of(Obiknovena).With(0, 4, 1).With(0, 2, 1).With(1, 20, 12).Build();
            Assert.True(StepRules.CanStep(high, 0, 4, 6));
            Assert.False(StepRules.CanStep(high, 0, 2, 6));
            Assert.True(StepRules.CanStep(high, 0, 2, 2));
        }

        [Fact]
        public void NothingIsBorneOffWhileACheckerIsOutsideTheHomeBoard()
        {
            var position = TestPosition.Of(Obiknovena).With(0, 7, 1).With(0, 2, 1).With(1, 20, 12).Build();

            Assert.False(StepRules.CanStep(position, 0, 2, 2));
            Assert.True(StepRules.CanStep(position, 0, 7, 1));
        }

        [Fact]
        public void TheLastCheckerBorneOffEndsTheGameWithTheRestOfTheRollUnplayed()
        {
            var position = TestPosition.Of(Obiknovena).With(0, 2, 1).With(1, 20, 12).Build();

            var (max, ends) = Generate(position, 0, StageDice.Distinct(6, 1));
            Assert.Equal(2, max);
            var end = Assert.Single(ends);
            Assert.Equal(GameEnd.BorneOff, end.End);
            Assert.Equal("2/6", end.Steps.ToString());

            var (maxDouble, endsDouble) = Generate(TestPosition.Of(Obiknovena).With(0, 1, 1).With(1, 20, 12).Build(), 0, StageDice.Same(3, 4));
            Assert.Equal(4, maxDouble);
            Assert.Equal("1/3", Assert.Single(endsDouble).Steps.ToString());
        }

        [Fact]
        public void ACheckerOnTheBarMustEnterBeforeAnyOtherMove()
        {
            var position = TestPosition.Of(Obiknovena).With(0, Geometry.Bar, 1).With(0, 13, 5).With(1, 19, 5).Build();

            Assert.False(StepRules.CanStep(position, 0, 13, 6));
            var (max, ends) = Generate(position, 0, StageDice.Distinct(6, 5));

            Assert.Equal(2, max);
            Assert.All(ends, end => Assert.Equal(Geometry.Bar, end.Steps.From(0)));
        }

        [Fact]
        public void NoCheckerEntersAgainstAClosedBoard()
        {
            var closed = TestPosition.Of(Obiknovena).With(0, Geometry.Bar, 1).With(0, 13, 5);
            for (var point = 1; point <= 6; point++)
            {
                closed.With(1, point, 2);
            }

            var position = closed.Build();
            foreach (var roll in PositionSources.AllRolls)
            {
                var (max, ends) = Generate(position, 0, roll);
                Assert.Equal(0, max);
                Assert.Equal(0, Assert.Single(ends).Steps.Count);
                Assert.Equal(position, ends[0].Position);
            }
        }

        [Fact]
        public void ALoneCheckerThatIsHitGoesToTheBar()
        {
            // Seat 1's own 15 is seat 0's 10.
            var position = TestPosition.Of(Obiknovena).With(0, 13, 1).With(1, 15, 1).Build();

            Assert.True(StepRules.CanStep(position, 0, 13, 3));
            Assert.Equal(GameEnd.None, StepRules.Apply(ref position, 0, 13, 3));

            Assert.Equal(1, position.Count(1, Geometry.Bar));
            Assert.Equal(0, position.Count(1, 15));
            Assert.Equal(1, position.Count(0, 10));
        }

        [Fact]
        public void TwoCheckersHoldAPoint()
        {
            var position = TestPosition.Of(Obiknovena).With(0, 13, 1).With(1, 15, 2).Build();

            Assert.False(StepRules.CanStep(position, 0, 13, 3));
        }

        [Fact]
        public void TheStartPositionsShouldBeTheStandardOnes()
        {
            Assert.Equal("O:-2,,,,,5,,3,,,,-5,5,,,,-3,,-5,,,,,2|0,0|0,0", PositionCode.Format(Position.Start(BackgammonVersion.Obiknovena)));
            Assert.Equal("C:-2,,,,,5,,3,,,,-5,5,,,,-3,,-5,,,,,2|0,0|0,0", PositionCode.Format(Position.Start(BackgammonVersion.Chelebi)));
            Assert.Equal("T:-15,,,,,,,,,,,,,,,,,,,,,,,15|0,0|0,0", PositionCode.Format(Position.Start(BackgammonVersion.Tapa)));
            Assert.Equal("G:,,,,,,,,,,,-15,,,,,,,,,,,,15|0,0|0,0", PositionCode.Format(Position.Start(BackgammonVersion.Gyulbara)));
        }

        [Fact]
        public void APositionCodeShouldReadBackToTheSamePosition()
        {
            var position = TestPosition.Of(BackgammonVersion.Tapa).With(0, 24, 3).Pinned(1, 15, 2).Pinned(0, 3).With(1, 24, 4).Build();

            var code = PositionCode.Format(position);

            Assert.Equal(position, PositionCode.Parse(code));
            Assert.Contains("2*", code);
            Assert.Contains("-1*", code);
        }

        internal static (int Max, List<StageEnd> Ends) Generate(in Position position, int seat, StageDice dice)
        {
            var ends = new List<StageEnd>();
            var max = new StageGenerator().Generate(position, seat, dice, ends);
            return (max, ends);
        }
    }
}
