namespace Backgammon.Logic.Tests.Vectors
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using Backgammon.Logic.Rules;
    using Xunit;

    /// <summary>
    /// The engine must reproduce the committed test vectors exactly. Ports of the rules (ednaigra.com's TypeScript
    /// board) check themselves against the same files, so a change here is a change of the rules. Regenerate the files
    /// with <c>tools/Backgammon.Arena vectors</c> only on purpose.
    /// </summary>
    public class TestVectorTests
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static TheoryData<string> MoveFiles => new(
            "moves-obiknovena.json", "moves-gyulbara.json", "moves-tapa.json", "moves-chelebi.json", "tricky.json");

        public static TheoryData<string> MatchFiles => new(
            "matches-obiknovena.json", "matches-gyulbara.json", "matches-tapa.json", "matches-chelebi.json", "matches-sreshta.json");

        [Theory]
        [MemberData(nameof(MoveFiles))]
        public void TheLegalPlaysOfEveryVectorPositionShouldMatch(string file)
        {
            using var document = Load(file);
            var generator = new StageGenerator();
            var ends = new List<StageEnd>();
            var cases = 0;
            foreach (var moveCase in document.RootElement.GetProperty("cases").EnumerateArray())
            {
                var position = PositionCode.Parse(moveCase.GetProperty("position").GetString()!);
                var seat = moveCase.GetProperty("seat").GetInt32();
                var dice = moveCase.GetProperty("dice").EnumerateArray().Select(d => d.GetInt32()).ToArray();
                var where = $"{file} case {cases} {PositionCode.Format(position)} seat {seat} dice {string.Join('-', dice)}";

                var playable = generator.Generate(position, seat, ViewConverter.ToStageDice(dice), ends);

                Assert.True(moveCase.GetProperty("playableDice").GetInt32() == playable, where);
                var expected = moveCase.GetProperty("ends").EnumerateArray().ToList();
                Assert.True(expected.Count == ends.Count, $"{where}: {expected.Count} ends expected, {ends.Count} found");
                for (var i = 0; i < ends.Count; i++)
                {
                    Assert.Equal(expected[i].GetProperty("position").GetString(), PositionCode.Format(ends[i].Position));
                    var steps = expected[i].GetProperty("steps").EnumerateArray().Select(s => $"{s[0].GetInt32()}/{s[1].GetInt32()}");
                    Assert.Equal(string.Join(' ', steps), ends[i].Steps.ToString());
                    Assert.Equal(expected[i].GetProperty("end").GetString(), JsonNamingPolicy.CamelCase.ConvertName(ends[i].End.ToString()));
                }

                cases++;
            }

            Assert.True(cases >= 25, $"{file} has only {cases} cases");
        }

        [Theory]
        [MemberData(nameof(MatchFiles))]
        public void EveryVectorMatchShouldReplayExactly(string file)
        {
            using var document = Load(file);
            foreach (var entry in document.RootElement.GetProperty("matches").EnumerateArray())
            {
                var options = entry.GetProperty("options");
                var draws = new Queue<JsonElement>(entry.GetProperty("draws").EnumerateArray());
                var match = new BackgammonMatch(new BackgammonMatchOptions
                {
                    Variant = Enum.Parse<BackgammonVariant>(options.GetProperty("variant").GetString()!, ignoreCase: true),
                    AutoPlayForcedStages = options.GetProperty("autoPlayForcedStages").GetBoolean(),
                    Dice = (n, purpose) =>
                    {
                        var draw = draws.Dequeue();
                        Assert.Equal(draw.GetProperty("purpose").GetString(), purpose);
                        Assert.Equal(draw.GetProperty("n").GetInt32(), n);
                        return draw.GetProperty("value").GetInt32();
                    },
                });
                Assert.Equal(options.GetProperty("targetPoints").GetInt32(), match.TargetPoints);

                var checkpoints = entry.GetProperty("checkpoints");
                var hasCheckpoints = checkpoints.ValueKind == JsonValueKind.Array;
                match.Start();
                var index = 0;
                if (hasCheckpoints)
                {
                    Assert.Equal(checkpoints[index].GetRawText(), Checkpoint(match));
                }

                foreach (var action in entry.GetProperty("actions").EnumerateArray())
                {
                    var seat = action.GetProperty("seat").GetInt32();
                    var steps = action.GetProperty("steps").EnumerateArray().Select(s => new BackgammonStep(s[0].GetInt32(), s[1].GetInt32())).ToArray();
                    Assert.Equal(seat, match.ToMove);
                    Assert.Equal(BackgammonActResult.Ok, match.Act(seat, new BackgammonAction { Steps = steps }));
                    index++;
                    if (hasCheckpoints)
                    {
                        Assert.Equal(checkpoints[index].GetRawText(), Checkpoint(match));
                    }
                }

                Assert.True(match.IsFinished);
                Assert.Empty(draws);
                Assert.Equal(entry.GetProperty("final").GetRawText(), JsonSerializer.Serialize(match.GetFinalView(), Json));
            }
        }

        private static string Checkpoint(BackgammonMatch match)
        {
            // The same projection as tools/Backgammon.Arena writes.
            var view = match.GetView(0);
            return JsonSerializer.Serialize(
                new
                {
                    view.Ply,
                    view.ToMove,
                    view.GameNumber,
                    view.Version,
                    view.Scores,
                    Board = PositionCode.Format(ViewConverter.ToPosition(view.Version, view.Board)),
                    view.Pips,
                    view.Roll,
                    view.RollSeat,
                    view.RollNumber,
                    view.StageDice,
                    view.StageNumber,
                    ChainRest = view.ChainRest.Select(s => new[] { s.Die, s.Count }),
                    view.IsEscalating,
                    view.IsPlayingRemainder,
                    view.RollsMade,
                    view.StuckRolls,
                    Results = view.Results.Count,
                },
                Json);
        }

        private static JsonDocument Load(string file)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "test-vectors")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "test-vectors", file)));
        }
    }
}
