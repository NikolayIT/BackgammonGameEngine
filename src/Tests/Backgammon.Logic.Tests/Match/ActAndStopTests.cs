namespace Backgammon.Logic.Tests.Match
{
    using System;
    using System.Linq;

    using Backgammon.Logic.Rules;
    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>Act, Validate and Stop: who may act, what is refused, and that a refusal changes nothing.</summary>
    public class ActAndStopTests
    {
        [Fact]
        public void ActingBeforeTheStartShouldThrow()
        {
            var match = new BackgammonMatch();

            Assert.Throws<InvalidOperationException>(() => match.Validate(0, new BackgammonAction()));
            Assert.Throws<InvalidOperationException>(() => match.Act(0, new BackgammonAction()));
            Assert.Equal(-1, match.ToMove);
        }

        [Fact]
        public void StartingTwiceShouldThrow()
        {
            var match = new BackgammonMatch();
            match.Start();

            Assert.Throws<InvalidOperationException>(match.Start);
        }

        [Fact]
        public void TheSeatNotToMoveShouldBeRefused()
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Script(6, 1).Source });
            match.Start();
            var steps = match.GetStageMoves().Outcomes[0].Steps;

            Assert.Equal(BackgammonActResult.NotYourTurn, match.Act(1, new BackgammonAction { Steps = steps }));
            Assert.Equal(BackgammonActResult.NotYourTurn, match.Act(2, new BackgammonAction { Steps = steps }));
            Assert.Equal(BackgammonActResult.NotYourTurn, match.Act(-1, new BackgammonAction { Steps = steps }));
            Assert.Equal(0, match.Ply);
        }

        [Fact]
        public void ActionsThatDoNotPlayTheStageLegallyShouldBeRefused()
        {
            // Seat 0 opens with 6-1 from the обикновена start; seat 1 then rolls 3-2.
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Script(6, 1, 3, 2).Source });
            match.Start();
            var before = Json.Serialize(match.GetFinalView());

            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, null!));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, new BackgammonAction { Steps = null! }));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, new BackgammonAction()));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((13, 6))));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((13, 6), (8, 6))));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((13, 6), (8, 1), (7, 1))));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((14, 6), (8, 1))));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((13, 5), (8, 2))));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((25, 6), (8, 1))));
            Assert.Equal(BackgammonActResult.Illegal, match.Act(0, MatchDriver.Steps((13, 6), (8, 1), (13, 6), (8, 1), (6, 1))));

            Assert.Equal(before, Json.Serialize(match.GetFinalView()));
            Assert.Equal(BackgammonActResult.Ok, match.Act(0, MatchDriver.Steps((13, 6), (7, 1))));
        }

        [Fact]
        public void ValidateShouldAgreeWithActAndChangeNothing()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var variant = (BackgammonVariant)(seed % 5);
                var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = LoggedDice.Seeded(seed).Source });
                match.Start();
                var random = new Random(seed);
                while (!match.IsFinished)
                {
                    var seat = match.ToMove;
                    var junk = RandomSteps(random);
                    var before = Json.Serialize(match.GetFinalView());
                    var verdict = match.Validate(seat, junk);
                    Assert.Equal(before, Json.Serialize(match.GetFinalView()));
                    Assert.Equal(match.GetStageMoves().IsLegal(junk.Steps) ? BackgammonActResult.Ok : BackgammonActResult.Illegal, verdict);
                    Assert.Equal(verdict, match.Act(seat, junk));
                    if (verdict != BackgammonActResult.Ok)
                    {
                        Assert.Equal(before, Json.Serialize(match.GetFinalView()));
                        Assert.Equal(BackgammonActResult.Ok, match.Act(seat, MatchDriver.RandomAction(match, random)));
                    }
                }
            }
        }

        [Fact]
        public void StopShouldEndTheMatchWithNoWinner()
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Seeded(2).Source });
            match.Start();
            MatchDriver.PlayFirst(match);
            var seat = match.ToMove;

            match.Stop();

            Assert.True(match.IsFinished);
            Assert.True(match.IsStopped);
            Assert.Equal(-1, match.Winner);
            Assert.Equal(-1, match.ToMove);
            Assert.Equal(BackgammonActResult.MatchFinished, match.Act(seat, new BackgammonAction()));
            Assert.Equal(BackgammonActResult.MatchFinished, match.Validate(seat, new BackgammonAction()));
            var record = match.GetRecord();
            Assert.True(record.IsStopped);
            Assert.Null(Assert.Single(record.Games).Result);
            Assert.Throws<InvalidOperationException>(match.GetStageMoves);

            match.Stop();
            Assert.True(match.IsStopped);
        }

        [Fact]
        public void AMatchStoppedMidTurnShouldStillAccountForEveryDie()
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var dice = LoggedDice.Seeded(seed);
                var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = (BackgammonVariant)(seed % 5), Dice = dice.Source });
                match.Start();
                var random = new Random(seed);
                for (var action = 0; action < 5 + seed && !match.IsFinished; action++)
                {
                    match.Act(match.ToMove, MatchDriver.RandomAction(match, random));
                }

                match.Stop();

                var record = match.GetRecord();
                Assert.Equal(dice.Log.Select(d => (d.Purpose, d.Die)), LoggedDice.DrawsOf(record));
                if (!record.Games[^1].Plays.Any(p => p.Stage == 0 && !p.IsRemainder && p.Roll.SequenceEqual(new[] { dice.Log[^2].Die, dice.Log[^1].Die })))
                {
                    Assert.NotEmpty(record.Games[^1].PendingRoll);
                }
            }
        }

        [Fact]
        public void StoppingAFinishedMatchShouldChangeNothing()
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Seeded(4).Source });
            match.Start();
            MatchDriver.PlayOut(match, new Random(4));
            var winner = match.Winner;

            match.Stop();

            Assert.False(match.IsStopped);
            Assert.Equal(winner, match.Winner);
        }

        [Fact]
        public void WithoutHistoryTheViewsHaveNoPlaysAndThereIsNoRecord()
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Seeded(4).Source, RecordHistory = false });
            match.Start();
            MatchDriver.PlayOut(match, new Random(4));

            Assert.Empty(match.GetView(0).Plays);
            Assert.Null(match.GetView(0).LastGame);
            Assert.NotEmpty(match.GetView(0).Results);
            Assert.Throws<InvalidOperationException>(() => match.GetRecord());
        }

        private static BackgammonAction RandomSteps(Random random)
        {
            var count = random.Next(0, 6);
            return new BackgammonAction { Steps = Enumerable.Range(0, count).Select(_ => new BackgammonStep(random.Next(-1, 27), random.Next(0, 8))).ToArray() };
        }
    }
}
