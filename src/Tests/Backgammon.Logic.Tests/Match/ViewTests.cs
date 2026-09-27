namespace Backgammon.Logic.Tests.Match
{
    using System;
    using System.Linq;
    using System.Text.Json;

    using Backgammon.Logic.Tests.Support;
    using Xunit;

    /// <summary>
    /// Views and records are plain JSON-serializable models. A record replays its match exactly, and the move helpers
    /// work from a view alone.
    /// </summary>
    public class ViewTests
    {
        [Theory]
        [InlineData(BackgammonVariant.Obiknovena)]
        [InlineData(BackgammonVariant.Gyulbara)]
        [InlineData(BackgammonVariant.Tapa)]
        [InlineData(BackgammonVariant.Chelebi)]
        [InlineData(BackgammonVariant.Sreshta)]
        public void EveryViewShouldSurviveAJsonRoundTrip(BackgammonVariant variant)
        {
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = LoggedDice.Seeded(11).Source });
            match.Start();
            var random = new Random(11);
            while (!match.IsFinished)
            {
                for (var seat = 0; seat < 2; seat++)
                {
                    var json = Json.Serialize(match.GetView(seat));
                    Assert.Equal(json, Json.Serialize(Json.Deserialize(json)));

                    var plain = JsonSerializer.Serialize(match.GetView(seat));
                    Assert.Equal(plain, JsonSerializer.Serialize(JsonSerializer.Deserialize<BackgammonSeatView>(plain)));
                }

                match.Act(match.ToMove, MatchDriver.RandomAction(match, random));
            }

            var final = Json.Serialize(match.GetFinalView());
            Assert.Equal(final, Json.Serialize(Json.Deserialize(final)));
            Assert.Contains("\"record\":{", final);
        }

        [Fact]
        public void AnActionShouldSurviveAJsonRoundTrip()
        {
            var action = MatchDriver.Steps((25, 3), (13, 5));

            var json = JsonSerializer.Serialize(action, HostJsonContext.Default.BackgammonAction);
            var back = JsonSerializer.Deserialize(json, HostJsonContext.Default.BackgammonAction)!;

            Assert.Equal("{\"steps\":[{\"from\":25,\"die\":3},{\"from\":13,\"die\":5}]}", json);
            Assert.Equal(action.Steps, back.Steps);
        }

        [Theory]
        [InlineData(BackgammonVariant.Obiknovena)]
        [InlineData(BackgammonVariant.Gyulbara)]
        [InlineData(BackgammonVariant.Tapa)]
        [InlineData(BackgammonVariant.Chelebi)]
        [InlineData(BackgammonVariant.Sreshta)]
        public void AMatchShouldReplayExactlyFromItsRecord(BackgammonVariant variant)
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var options = new BackgammonMatchOptions { Variant = variant, Dice = LoggedDice.Seeded(seed).Source, AutoPlayForcedStages = seed % 2 == 0 };
                var original = new BackgammonMatch(options);
                original.Start();
                MatchDriver.PlayOut(original, new Random(seed));
                var record = original.GetRecord();

                // The dice come from the record alone, and every play not made by the engine is acted again.
                var replayDice = LoggedDice.Script(LoggedDice.DrawsOf(record).Select(d => d.Die).ToArray());
                var replay = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = replayDice.Source, AutoPlayForcedStages = options.AutoPlayForcedStages });
                replay.Start();
                foreach (var play in record.Games.SelectMany(g => g.Plays).Where(p => !p.Auto))
                {
                    Assert.Equal(play.Seat, replay.ToMove);
                    Assert.Equal(BackgammonActResult.Ok, replay.Act(play.Seat, new BackgammonAction { Steps = play.Steps }));
                }

                Assert.Equal(0, replayDice.ScriptLeft);
                Assert.Equal(Json.Serialize(original.GetFinalView()), Json.Serialize(replay.GetFinalView()));
            }
        }

        [Fact]
        public void TheMoveHelpersShouldGuideAPartialStage()
        {
            // Seat 0 opens with 6-1 from the обикновена start.
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Script(6, 1).Source });
            match.Start();
            var moves = BackgammonStageMoves.For(match.GetView(0));

            Assert.Equal(2, moves.PlayableDice);
            Assert.Equal(2, moves.DiceLeft(Array.Empty<BackgammonStep>()));
            var first = moves.NextSteps();
            Assert.Contains(first, o => o.Step == new BackgammonStep(13, 6) && o.To == 7 && o.Seat0To == 7);
            Assert.Contains(first, o => o.Step == new BackgammonStep(24, 1) && o.To == 23);
            Assert.DoesNotContain(first, o => o.Step.From == 6 && o.Step.Die == 6);

            var partial = new[] { new BackgammonStep(13, 6) };
            Assert.True(moves.IsLegalPrefix(partial));
            Assert.False(moves.IsLegal(partial));
            Assert.Equal(1, moves.DiceLeft(partial));
            var fromSeven = moves.Destinations(partial, 7);
            var step = Assert.Single(fromSeven);
            Assert.Equal((new BackgammonStep(7, 1), 6), (step.Step, step.To));
            Assert.All(moves.NextSteps(partial), o => Assert.Equal(1, o.Step.Die));
            Assert.Equal(0, moves.DiceLeft(new[] { new BackgammonStep(13, 6), new BackgammonStep(7, 1) }));
            Assert.Throws<ArgumentException>(() => moves.DiceLeft(new[] { new BackgammonStep(14, 6) }));

            Assert.Equal(moves.OutcomeCount, match.GetStageMoves().OutcomeCount);
            Assert.Equal(moves.OutcomeCount, moves.Outcomes.Select(o => string.Join(",", o.Board.Points.Select(p => p.Seat0 - p.Seat1))).Distinct().Count());
        }

        [Fact]
        public void TheSeatOneHelpersShouldNumberBothWays()
        {
            // Seat 1 wins the opening with 5-2 (seat 0's die first).
            var match = new BackgammonMatch(new BackgammonMatchOptions { Dice = LoggedDice.Script(2, 5).Source });
            match.Start();
            var moves = match.GetStageMoves();

            var option = moves.NextSteps().First(o => o.Step == new BackgammonStep(13, 5));
            Assert.Equal((8, 12, 17), (option.To, option.Seat0From, option.Seat0To));
        }
    }
}
