namespace Backgammon.Logic.Tests.Match
{
    using System;
    using System.Linq;

    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// The opening roll and the exact order of the dice draws: a host checks every die against its provably fair
    /// stream, so the order is part of the contract.
    /// </summary>
    public class OpeningAndDiceTests
    {
        [Fact]
        public void TiesAreRolledAgainAndTheHigherDieStartsWithBothDice()
        {
            var dice = LoggedDice.Script(3, 3, 2, 5, 6, 1);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = dice.Source });

            match.Start();

            var view = match.GetView(0);
            Assert.Equal(1, view.ToMove);
            Assert.Equal(new[] { 2, 5 }, view.Roll);
            Assert.Equal(new[] { 2, 5 }, view.StageDice);
            Assert.Equal(1, view.RollSeat);
            Assert.Equal(1, view.RollNumber);
            Assert.Equal(new[] { 0, 1 }, view.RollsMade);
            Assert.Equal(4, dice.Log.Count);
            Assert.All(dice.Log, draw => Assert.Equal((BackgammonMatchOptions.OpeningPurpose, 6), (draw.Purpose, draw.N)));

            MatchDriver.PlayFirst(match);

            // Seat 0's first roll is drawn only now, as two "dice" draws.
            Assert.Equal(6, dice.Log.Count);
            Assert.Equal(new[] { BackgammonMatchOptions.DicePurpose, BackgammonMatchOptions.DicePurpose }, dice.Log.Skip(4).Select(d => d.Purpose));
            view = match.GetView(1);
            Assert.Equal(0, view.ToMove);
            Assert.Equal(new[] { 6, 1 }, view.Roll);
            Assert.Equal(new[] { 1, 1 }, view.RollsMade);

            var game = match.GetFinalView().Record!.Games.Single();
            Assert.Equal(new[] { new[] { 3, 3 }, new[] { 2, 5 } }, game.Openings.Select(p => p.ToArray()));
            Assert.Equal(1, game.Starter);
            var opening = game.Plays[0];
            Assert.True(opening.IsOpeningRoll);
            Assert.Equal(1, opening.Seat);
            Assert.Equal(new[] { 2, 5 }, opening.Roll);
            Assert.Equal(1, opening.RollNumber);
            Assert.Equal(1, opening.Ply);
        }

        [Fact]
        public void TheFirstDieOfEachOpeningPairIsSeatZeros()
        {
            var dice = LoggedDice.Script(6, 2, 1, 1);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = dice.Source });

            match.Start();

            Assert.Equal(0, match.ToMove);
            Assert.Equal(new[] { 6, 2 }, match.GetView(0).Roll);
            Assert.Equal(new[] { 6, 2 }, match.GetView(0).StageDice);
        }

        [Fact]
        public void TheDiceSourceMustReturnAValueBelowN()
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = (n, _) => n });

            Assert.Throws<InvalidOperationException>(match.Start);
        }

        [Theory]
        [InlineData(BackgammonVariant.Obiknovena)]
        [InlineData(BackgammonVariant.Gyulbara)]
        [InlineData(BackgammonVariant.Tapa)]
        [InlineData(BackgammonVariant.Chelebi)]
        [InlineData(BackgammonVariant.Sreshta)]
        public void EveryDrawShouldBeInTheRecordInTheDocumentedOrder(BackgammonVariant variant)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var dice = LoggedDice.Seeded(seed);
                var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = dice.Source });
                match.Start();
                var random = new Random(seed);
                while (!match.IsFinished)
                {
                    // At every decision, the draws so far are exactly those the record accounts for: no die is drawn
                    // ahead of its roll.
                    Assert.Equal(dice.Log.Select(d => (d.Purpose, d.Die)), LoggedDice.DrawsOf(match.GetFinalView().Record!));
                    Assert.Equal(BackgammonActResult.Ok, match.Act(match.ToMove, MatchDriver.RandomAction(match, random)));
                }

                Assert.Equal(dice.Log.Select(d => (d.Purpose, d.Die)), LoggedDice.DrawsOf(match.GetRecord()));
                Assert.All(dice.Log, draw => Assert.Equal(6, draw.N));
            }
        }
    }
}
