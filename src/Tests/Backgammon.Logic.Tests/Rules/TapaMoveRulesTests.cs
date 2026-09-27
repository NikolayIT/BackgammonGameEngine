namespace Backgammon.Logic.Tests.Rules
{
    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// Тапа's own rules: pinning, releasing, the майка and the double-mother draw, and no bearing off while pinned.
    /// Seat 1's own point p is seat 0's 25 - p; each player starts on its own 24, the other's 1.
    /// </summary>
    public class TapaMoveRulesTests
    {
        private const BackgammonVersion Tapa = BackgammonVersion.Tapa;

        [Fact]
        public void LandingOnALoneCheckerPinsItInsteadOfHittingIt()
        {
            var position = TestPosition.Of(Tapa).With(0, 13, 1).With(1, 15, 1).With(1, 24, 13).Build();

            Assert.Equal(GameEnd.None, StepRules.Apply(ref position, 0, 13, 3));

            Assert.Equal(1, position.Count(1, 15));
            Assert.True(position.IsPinned(1, 15));
            Assert.Equal(1, position.Count(0, 10));
            Assert.Equal(0, position.Count(1, Geometry.Bar));
        }

        [Fact]
        public void APinnedCheckerCannotMove()
        {
            var position = TestPosition.Of(Tapa).Pinned(1, 15).With(1, 24, 13).Build();

            for (var die = 1; die <= 6; die++)
            {
                Assert.False(StepRules.CanStep(position, 1, 15, die));
            }

            Assert.True(StepRules.CanStep(position, 1, 24, 1));
        }

        [Fact]
        public void ThePinIsReleasedWhenTheLastPinnerLeaves()
        {
            var position = TestPosition.Of(Tapa).Pinned(1, 15, pinners: 2).With(1, 24, 13).Build();

            StepRules.Apply(ref position, 0, 10, 2);
            Assert.True(position.IsPinned(1, 15));

            StepRules.Apply(ref position, 0, 10, 2);
            Assert.False(position.IsPinned(1, 15));
            Assert.True(StepRules.CanStep(position, 1, 15, 1));
        }

        [Fact]
        public void APinnerCanBeJoinedButNeverPinned()
        {
            var position = TestPosition.Of(Tapa).Pinned(1, 15).With(0, 13, 1).With(1, 17, 1).With(1, 24, 12).Build();

            // Seat 0 may stack on its own pinning point...
            Assert.True(StepRules.CanStep(position, 0, 13, 3));

            // ...but seat 1 may not land where its own checker is pinned: seat 0 holds that point.
            Assert.False(StepRules.CanStep(position, 1, 17, 2));
        }

        [Fact]
        public void TwoCheckersBlockAPoint()
        {
            var position = TestPosition.Of(Tapa).With(0, 13, 1).With(1, 15, 2).With(1, 24, 13).Build();

            Assert.False(StepRules.CanStep(position, 0, 13, 3));
        }

        [Fact]
        public void PinningTheOpponentsMotherWithAnEmptyOwnStartWinsAtOnce()
        {
            // Seat 1's mother: its last checker on its own 24, which is seat 0's 1.
            var position = TestPosition.Of(Tapa).With(0, 3, 1).With(0, 13, 5).With(1, 24, 1).With(1, 10, 5).Build();

            Assert.Equal(GameEnd.Mother, StepRules.Apply(ref position, 0, 3, 2));
        }

        [Fact]
        public void PinningTheMotherWhileOwnStartIsOccupiedWinsOnlyOnceTheStartIsEmpty()
        {
            var position = TestPosition.Of(Tapa).With(0, 24, 2).With(0, 3, 1).With(1, 24, 1).With(1, 10, 5).Build();

            Assert.Equal(GameEnd.None, StepRules.Apply(ref position, 0, 3, 2));
            Assert.True(position.IsPinned(1, 24));

            Assert.Equal(GameEnd.None, StepRules.Apply(ref position, 0, 24, 5));
            Assert.Equal(GameEnd.Mother, StepRules.Apply(ref position, 0, 24, 5));
        }

        [Fact]
        public void BothMothersPinnedIsADraw()
        {
            // Seat 1's mother is pinned while seat 0 still has its own mother home; seat 1 then pins that one.
            var position = TestPosition.Of(Tapa).Pinned(1, 24).With(0, 24, 1).With(0, 13, 5).With(1, 3, 1).With(1, 10, 5).Build();

            Assert.Equal(GameEnd.BothMothers, StepRules.Apply(ref position, 1, 3, 2));
        }

        [Fact]
        public void APinnedCheckerInTheHomeBoardStopsAllBearingOff()
        {
            var position = TestPosition.Of(Tapa).With(0, 6, 2).With(0, 3, 1).Pinned(0, 2).With(1, 10, 5).Build();

            Assert.False(StepRules.CanStep(position, 0, 6, 6));
            Assert.False(StepRules.CanStep(position, 0, 3, 3));
            Assert.True(StepRules.CanStep(position, 0, 6, 1));

            var free = TestPosition.Of(Tapa).With(0, 6, 2).With(0, 3, 1).With(0, 2, 1).With(1, 10, 5).Build();
            Assert.True(StepRules.CanStep(free, 0, 6, 6));
        }

        [Fact]
        public void AMotherPinnedInTheOpponentsHomeStillCountsAsItsCheckerOutsideHome()
        {
            // Seat 0's mother pinned on its own 24 keeps seat 0 from bearing off at all.
            var position = TestPosition.Of(Tapa).With(0, 6, 2).Pinned(0, 24).With(1, 10, 5).Build();

            Assert.False(StepRules.CanStep(position, 0, 6, 6));
        }
    }
}
