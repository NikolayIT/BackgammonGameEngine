namespace Backgammon.Arena
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// Writes the JSON test vectors in test-vectors/ (see its README.md): the legal plays of many positions and rolls
    /// per version, the tricky positions, and whole recorded matches per variant. Everything comes from fixed seeds, so
    /// running it again must reproduce the files byte for byte.
    /// </summary>
    internal static class VectorWriter
    {
        public const int PositionsPerVersion = 25;
        public const int MatchesPerVariant = 3;

        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static void WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var version in Enum.GetValues<BackgammonVersion>())
            {
                var cases = SamplePositions(version, PositionsPerVersion, seed: 1000 + (int)version)
                    .SelectMany((sample, index) => Cases(sample.Position, sample.Seat, new Random(index)))
                    .ToList();
                Write(Path.Combine(folder, $"moves-{Name(version)}.json"), $"{{\"version\":\"{Name(version)}\",\"rulesVersion\":{BackgammonMatchRecord.CurrentRulesVersion},\"cases\":[", cases, "]}");
            }

            var tricky = Scenarios.All().Select(scenario =>
            {
                var moveCase = Case(scenario.Build(), scenario.Seat, scenario.Dice);
                return $"{{\"name\":{JsonSerializer.Serialize(scenario.Name, Json)},\"description\":{JsonSerializer.Serialize(scenario.Description, Json)},{moveCase[1..]}";
            }).ToList();
            Write(Path.Combine(folder, "tricky.json"), $"{{\"rulesVersion\":{BackgammonMatchRecord.CurrentRulesVersion},\"cases\":[", tricky, "]}");

            foreach (var variant in Enum.GetValues<BackgammonVariant>())
            {
                var matches = Enumerable.Range(1, MatchesPerVariant).Select(seed => RecordMatch(variant, seed, withViews: seed == 1)).ToList();
                Write(Path.Combine(folder, $"matches-{Name(variant)}.json"), $"{{\"variant\":\"{Name(variant)}\",\"rulesVersion\":{BackgammonMatchRecord.CurrentRulesVersion},\"matches\":[", matches, "]}");
            }
        }

        public static string Name<TEnum>(TEnum value)
            where TEnum : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

        /// <summary>Positions from random matches of the version, with the seat to move.</summary>
        public static List<(Position Position, int Seat)> SamplePositions(BackgammonVersion version, int count, int seed)
        {
            var samples = new List<(Position Position, int Seat)>();
            var random = new Random(seed);
            for (var game = 0; samples.Count < count; game++)
            {
                var match = new BackgammonMatch(new BackgammonMatchOptions
                {
                    Variant = (BackgammonVariant)(int)version,
                    TargetPoints = 1,
                    RecordHistory = false,
                    Dice = (n, _) => random.Next(n),
                });
                match.Start();
                while (!match.IsFinished && samples.Count < count)
                {
                    if (random.Next(12) == 0)
                    {
                        samples.Add((match.CurrentGame!.Position, match.ToMove));
                    }

                    var moves = match.GetStageMoves();
                    match.Act(match.ToMove, new BackgammonAction { Steps = moves.Outcomes[random.Next(moves.OutcomeCount)].Steps });
                }
            }

            return samples;
        }

        private static IEnumerable<string> Cases(Position position, int seat, Random random)
        {
            for (var high = 1; high <= 6; high++)
            {
                for (var low = 1; low <= high; low++)
                {
                    yield return Case(position, seat, high == low ? new[] { high, high, high, high } : new[] { high, low });
                }
            }

            var die = random.Next(1, 7);
            yield return Case(position, seat, Enumerable.Repeat(die, random.Next(1, 4)).ToArray());
        }

        private static string Case(Position position, int seat, int[] dice)
        {
            var ends = new List<StageEnd>();
            var playable = new StageGenerator().Generate(position, seat, ViewConverter.ToStageDice(dice), ends);
            var text = new StringBuilder();
            text.Append("{\"position\":\"").Append(PositionCode.Format(position)).Append("\",\"seat\":").Append(seat)
                .Append(",\"dice\":[").Append(string.Join(',', dice)).Append("],\"playableDice\":").Append(playable).Append(",\"ends\":[");
            for (var i = 0; i < ends.Count; i++)
            {
                var end = ends[i];
                text.Append(i == 0 ? string.Empty : ",").Append("{\"position\":\"").Append(PositionCode.Format(end.Position)).Append("\",\"steps\":[");
                for (var step = 0; step < end.Steps.Count; step++)
                {
                    text.Append(step == 0 ? string.Empty : ",").Append('[').Append(end.Steps.From(step)).Append(',').Append(end.Steps.Die(step)).Append(']');
                }

                text.Append("],\"end\":\"").Append(JsonNamingPolicy.CamelCase.ConvertName(end.End.ToString())).Append("\"}");
            }

            return text.Append("]}").ToString();
        }

        private static string RecordMatch(BackgammonVariant variant, int seed, bool withViews)
        {
            var random = new Random(seed);
            var draws = new List<object>();
            var options = new BackgammonMatchOptions
            {
                Variant = variant,
                AutoPlayForcedStages = seed == MatchesPerVariant,
                Dice = (n, purpose) =>
                {
                    var value = random.Next(n);
                    draws.Add(new { purpose, n, value });
                    return value;
                },
            };
            var match = new BackgammonMatch(options);
            match.Start();
            var actions = new List<object>();
            var checkpoints = new List<object> { Checkpoint(match) };
            while (!match.IsFinished)
            {
                var seat = match.ToMove;
                var moves = match.GetStageMoves();
                var steps = moves.Outcomes[random.Next(moves.OutcomeCount)].Steps;
                match.Act(seat, new BackgammonAction { Steps = steps });
                actions.Add(new { seat, steps = steps.Select(s => new[] { s.From, s.Die }) });
                if (withViews)
                {
                    checkpoints.Add(Checkpoint(match));
                }
            }

            var entry = new Dictionary<string, object?>
            {
                ["seed"] = seed,
                ["options"] = new { variant, targetPoints = match.TargetPoints, autoPlayForcedStages = options.AutoPlayForcedStages },
                ["draws"] = draws,
                ["actions"] = actions,
                ["checkpoints"] = withViews ? checkpoints : null,
                ["final"] = match.GetFinalView(),
            };
            return JsonSerializer.Serialize(entry, Json);
        }

        // What a player sees after each action, in brief: enough to check a replay step by step without repeating the
        // whole history in every entry (the final view carries the full record).
        private static object Checkpoint(BackgammonMatch match)
        {
            var view = match.GetView(0);
            return new
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
            };
        }

        private static void Write(string path, string head, IReadOnlyList<string> items, string tail)
        {
            var text = new StringBuilder(head).Append('\n');
            for (var i = 0; i < items.Count; i++)
            {
                text.Append(items[i]).Append(i + 1 < items.Count ? ",\n" : "\n");
            }

            text.Append(tail).Append('\n');
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"{Path.GetFileName(path)}: {items.Count} entries, {new FileInfo(path).Length / 1024} KB");
        }
    }
}
