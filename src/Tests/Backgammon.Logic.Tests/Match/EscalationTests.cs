namespace Backgammon.Logic.Tests.Match
{
    using System.Linq;

    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// Гюлбара's (and челеби's) escalating doubles: from each player's 4th roll, n-n is played as four n's, four
    /// (n+1)'s, … up to four 6's. The rest of a chain the roller cannot finish goes to the opponent, stage by stage;
    /// a chain the roller cannot even start is lost.
    /// </summary>
    public class EscalationTests
    {
        [Theory]
        [InlineData(BackgammonVariant.Gyulbara)]
        [InlineData(BackgammonVariant.Chelebi)]
        public void DoublesEscalateFromEachPlayersFourthRollCountingTheOpeningAsTheStartersFirst(BackgammonVariant variant)
        {
            // Seat 0 wins the opening (roll 1); then 1:2-2, 0:5-4, 1:6-5, 0:1-1 (its 3rd), 1:4-3, 0:2-2 (its 4th).
            var dice = LoggedDice.Script(3, 1, 2, 2, 5, 4, 6, 5, 1, 1, 4, 3, 2, 2);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = dice.Source });
            match.Start();
            MatchDriver.PlayFirst(match);

            var view = match.GetView(1);
            Assert.Equal((1, 1), (view.ToMove, view.RollNumber));
            Assert.Equal(new[] { 2, 2, 2, 2 }, view.StageDice);
            Assert.False(view.IsEscalating);
            Assert.Empty(view.ChainRest);
            MatchDriver.PlayFirst(match);
            MatchDriver.PlayFirst(match);
            MatchDriver.PlayFirst(match);

            view = match.GetView(0);
            Assert.Equal((0, 3), (view.ToMove, view.RollNumber));
            Assert.Equal(new[] { 1, 1, 1, 1 }, view.StageDice);
            Assert.False(view.IsEscalating);
            Assert.Equal(new[] { true, false }, view.NextRollEscalates);
            MatchDriver.PlayFirst(match);
            MatchDriver.PlayFirst(match);

            view = match.GetView(0);
            Assert.Equal((0, 4), (view.ToMove, view.RollNumber));
            Assert.True(view.IsEscalating);
            Assert.Equal(new[] { 2, 2, 2, 2 }, view.StageDice);
            Assert.Equal(new[] { (3, 4), (4, 4), (5, 4), (6, 4) }, view.ChainRest.Select(s => (s.Die, s.Count)));

            MatchDriver.PlayFirst(match);
            view = match.GetView(0);
            Assert.Equal((0, 1), (view.ToMove, view.StageNumber));
            Assert.Equal(new[] { 3, 3, 3, 3 }, view.StageDice);
            Assert.Equal(3, view.ChainRest.Count);
            Assert.Equal(0, dice.ScriptLeft);
        }

        [Theory]
        [InlineData(BackgammonVariant.Obiknovena)]
        [InlineData(BackgammonVariant.Tapa)]
        public void DoublesNeverEscalateInObiknovenaOrTapa(BackgammonVariant variant)
        {
            var version = (BackgammonVersion)(int)variant;
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = LoggedDice.Script(2, 2).Source });

            match.StartAt(Position.Start(version), 0, 2, 2, new[] { 7, 6 });

            var view = match.GetView(0);
            Assert.False(view.IsEscalating);
            Assert.Empty(view.ChainRest);
            Assert.Equal(new[] { false, false }, view.NextRollEscalates);
        }

        [Fact]
        public void TheRestOfAChainTheRollerCannotFinishGoesToTheOpponent()
        {
            // Челеби: seat 0's lone checker plays 24/6 18/6, then seat 1 holds seat 0's 6: two 6s are left over.
            var position = TestPosition.Of(BackgammonVersion.Chelebi).With(0, 24, 1).With(1, 19, 2).With(1, 24, 13).Build();
            var dice = LoggedDice.Script(5, 3);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Chelebi, Dice = dice.Source });
            match.StartAt(position, 0, 6, 6, new[] { 4, 3 });

            Assert.Equal(0, match.ToMove);
            Assert.Equal(2, match.GetStageMoves().PlayableDice);
            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((24, 6), (18, 6))));

            var view = match.GetView(1);
            Assert.Equal(1, view.ToMove);
            Assert.True(view.IsPlayingRemainder);
            Assert.Equal(0, view.RollSeat);
            Assert.Equal(new[] { 6, 6 }, view.Roll);
            Assert.Equal(new[] { 6, 6 }, view.StageDice);
            Assert.Empty(view.ChainRest);
            Assert.Empty(dice.Log);

            MatchDriver.PlayFirst(match);

            // Having played the remainder, seat 1 rolls; the remainder did not count as one of its rolls.
            view = match.GetView(1);
            Assert.False(view.IsPlayingRemainder);
            Assert.Equal((1, 1), (view.ToMove, view.RollSeat));
            Assert.Equal(new[] { 5, 3 }, view.Roll);
            Assert.Equal(new[] { 4, 4 }, view.RollsMade);
            var remainder = view.Plays.Single(p => p.IsRemainder);
            Assert.Equal((1, 0, 4), (remainder.Seat, remainder.Stage, remainder.RollNumber));
            Assert.Equal(new[] { 6, 6 }, remainder.Dice);
        }

        [Fact]
        public void AChainTheRollerCannotStartIsLost()
        {
            // Seat 1 holds seat 0's 19: no 5 can be played, though a 6 could. Nothing passes to seat 1.
            var position = TestPosition.Of(BackgammonVersion.Chelebi).With(0, 24, 1).With(1, 6, 2).With(1, 24, 13).Build();
            var dice = LoggedDice.Script(4, 1);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Chelebi, Dice = dice.Source });

            match.StartAt(position, 0, 5, 5, new[] { 4, 3 });

            var view = match.GetView(1);
            Assert.Equal(1, view.ToMove);
            Assert.False(view.IsPlayingRemainder);
            Assert.Equal(new[] { 4, 1 }, view.Roll);
            var pass = Assert.Single(view.Plays);
            Assert.True(pass.Auto);
            Assert.Empty(pass.Steps);
            Assert.Equal(new[] { 5, 5, 5, 5 }, pass.Dice);
        }

        [Fact]
        public void TheOpponentPlaysTheRemainderStageByStageLosingWhatItCannotPlay()
        {
            // Гюлбара. Seat 0 plays one 5 (24/5; 19/5 is held by seat 1's single checker), so three 5s and four 6s go
            // to seat 1. Seat 1 can play no 5 (seat 0 holds its 15) but can play the 6s.
            var position = TestPosition.Of(BackgammonVersion.Gyulbara).With(0, 24, 1).With(0, 3, 1).With(1, 2, 1).With(1, 20, 1).Build();
            var dice = LoggedDice.Script(2, 1);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Gyulbara, Dice = dice.Source });
            match.StartAt(position, 0, 5, 5, new[] { 4, 3 });

            Assert.Equal(1, match.GetStageMoves().PlayableDice);
            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((24, 5))));

            var view = match.GetView(1);
            Assert.Equal(1, view.ToMove);
            Assert.True(view.IsPlayingRemainder);
            Assert.Equal(1, view.StageNumber);
            Assert.Equal(new[] { 6, 6, 6, 6 }, view.StageDice);
            var lost = view.Plays.Single(p => p.IsRemainder);
            Assert.Equal(new[] { 5, 5, 5 }, lost.Dice);
            Assert.True(lost.Auto);
            Assert.Empty(lost.Steps);

            MatchDriver.PlayFirst(match);
            Assert.Equal(new[] { 2, 1 }, match.GetView(1).Roll);
            Assert.Equal(1, match.GetView(1).RollSeat);
        }

        [Fact]
        public void AnOrdinaryDoubleLosesWhatCannotBePlayed()
        {
            // Obiknovena has no escalation: 24/6 18/6, and the other two 6s are simply lost.
            var position = TestPosition.Of(BackgammonVersion.Obiknovena).With(0, 24, 1).With(1, 19, 2).With(1, 24, 13).Build();
            var dice = LoggedDice.Script(5, 3);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = dice.Source });
            match.StartAt(position, 0, 6, 6, new[] { 4, 3 });

            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((24, 6), (18, 6))));

            var view = match.GetView(1);
            Assert.False(view.IsPlayingRemainder);
            Assert.Equal(new[] { 5, 3 }, view.Roll);
        }

        [Fact]
        public void AForcedStageIsPlayedByTheEngineOnlyWhenAutoPlayIsOn()
        {
            var position = TestPosition.Of(BackgammonVersion.Chelebi).With(0, 24, 1).With(1, 19, 2).With(1, 24, 13).Build();

            var manual = new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Chelebi, Dice = LoggedDice.Script(5, 3).Source });
            manual.StartAt(position, 0, 6, 6, new[] { 4, 3 });
            Assert.Equal(0, manual.ToMove);

            var auto = new BackgammonMatch(new BackgammonMatchOptions { Variant = BackgammonVariant.Chelebi, Dice = LoggedDice.Script(5, 3).Source, AutoPlayForcedStages = true });
            auto.StartAt(position, 0, 6, 6, new[] { 4, 3 });
            Assert.Equal(1, auto.ToMove);
            var play = Assert.Single(auto.GetView(1).Plays);
            Assert.True(play.Auto);
            Assert.Equal(new[] { new BackgammonStep(24, 6), new BackgammonStep(18, 6) }, play.Steps);
        }
    }
}
