namespace Backgammon.Logic.Tests.Rules
{
    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>Гюлбара's own rules: one checker holds a point, and the geometry of the diagonal starts.</summary>
    public class GyulbaraMoveRulesTests
    {
        private const BackgammonVersion Gyulbara = BackgammonVersion.Gyulbara;

        [Fact]
        public void ASingleCheckerHoldsAPoint()
        {
            // Seat 1's own 22 is seat 0's 10.
            var position = TestPosition.Of(Gyulbara).With(0, 13, 1).With(1, 22, 1).Build();

            Assert.Equal(10, Geometry.ToSeat0(Gyulbara, 1, 22));
            Assert.False(StepRules.CanStep(position, 0, 13, 3));
            Assert.True(StepRules.CanStep(position, 0, 13, 2));
        }

        [Fact]
        public void TheStartsShouldBeDiagonallyOppositeAndSeat1sHomeSeat0s13To18()
        {
            Assert.Equal(12, Geometry.ToSeat0(Gyulbara, 1, 24));
            Assert.Equal(1, Geometry.ToSeat0(Gyulbara, 1, 13));
            Assert.Equal(24, Geometry.ToSeat0(Gyulbara, 1, 12));
            Assert.Equal(13, Geometry.ToSeat0(Gyulbara, 1, 1));
            Assert.Equal(18, Geometry.ToSeat0(Gyulbara, 1, 6));
            for (var point = 1; point <= 24; point++)
            {
                Assert.Equal(point, Geometry.FromSeat0(Gyulbara, 1, Geometry.ToSeat0(Gyulbara, 1, point)));
            }
        }

        [Fact]
        public void FromTheStartASixCannotPassTheOpponentsStart()
        {
            // Seat 0 plays 6-6 from 24: 24/6 to 18 is open, 18/6 would land on seat 1's start (seat 0's 12).
            var (max, ends) = MoveRulesTests.Generate(Position.Start(Gyulbara), 0, StageDice.Same(6, 4));

            Assert.Equal(4, max);
            var end = Assert.Single(ends);
            Assert.Equal(4, end.Position.Count(0, 18));
            Assert.Equal("24/6 24/6 24/6 24/6", end.Steps.ToString());
        }

        [Fact]
        public void BothPlayersMoveTheSameWayRound()
        {
            // Seat 1's checkers leave seat 0's 12 towards seat 0's 1, just like seat 0's leave 24 towards 13.
            var position = Position.Start(Gyulbara);
            StepRules.Apply(ref position, 1, 24, 5);

            Assert.Equal(1, position.Count(1, 19));
            Assert.Equal(7, Geometry.ToSeat0(Gyulbara, 1, 19));
        }
    }
}
