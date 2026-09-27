namespace Backgammon.Arena
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;

    using Backgammon.Logic;

    /// <summary>The arena's commands: head-to-head runs, the rating ladder of the levels, and decision timing.</summary>
    internal static class Commands
    {
        public static IEnumerable<BackgammonVariant> Variants(string name) =>
            name == "all" ? Enum.GetValues<BackgammonVariant>() : new[] { Enum.Parse<BackgammonVariant>(name, ignoreCase: true) };

        /// <summary><c>arena &lt;variant|all&gt; &lt;A&gt; &lt;B&gt; [pairs] [threads]</c>: A against B in duplicate pairs.</summary>
        public static void Arena(string[] args)
        {
            var a = ArenaPlayer.Parse(args[2]);
            var b = ArenaPlayer.Parse(args[3]);
            var pairs = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 500;
            var threads = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : Environment.ProcessorCount;
            foreach (var variant in Variants(args[1]))
            {
                var clock = Stopwatch.StartNew();
                var stats = ArenaRunner.PlayPairs(variant, a, b, pairs, threads);
                Console.WriteLine($"{variant,-10} {stats} [{clock.Elapsed.TotalSeconds:F0}s]");
            }
        }

        /// <summary>
        /// <c>ladder &lt;variant|all&gt; [pairs] [players...]</c>: a round robin of the players (by default random and
        /// L1..L6), fitted to Bradley-Terry ratings with L1 at 800.
        /// </summary>
        public static void Ladder(string[] args)
        {
            var pairs = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 200;
            var names = args.Length > 3 ? args[3..] : new[] { "random", "L1", "L2", "L3", "L4", "L5", "L6" };
            var players = names.Select(ArenaPlayer.Parse).ToArray();
            foreach (var variant in Variants(args[1]))
            {
                var clock = Stopwatch.StartNew();
                var wins = new double[players.Length, players.Length];
                for (var i = 0; i < players.Length; i++)
                {
                    for (var j = i + 1; j < players.Length; j++)
                    {
                        var stats = ArenaRunner.PlayPairs(variant, players[i], players[j], pairs, Environment.ProcessorCount, seedBase: 1 + (1000 * ((i * players.Length) + j)));
                        wins[i, j] = stats.WinsA;
                        wins[j, i] = stats.Matches - stats.WinsA;
                        Console.WriteLine($"  {variant,-10} {stats}");
                    }
                }

                var ratings = BradleyTerry.Fit(wins);
                var anchor = Array.IndexOf(names, "L1") is var l1 && l1 >= 0 ? ratings[l1] : ratings[0];
                Console.WriteLine($"{variant} ratings [{clock.Elapsed.TotalMinutes:F1} min]:");
                for (var i = 0; i < players.Length; i++)
                {
                    Console.WriteLine($"  {names[i],-10} {800 + ratings[i] - anchor,7:F0}");
                }

                SuggestNoises(variant, names, ratings);
            }
        }

        /// <summary>
        /// <c>calibrate &lt;variant|all&gt; [pairs]</c>: finds the noise of each level of a version.
        /// <list type="number">
        /// <item>A chain of players runs from random play, through the strongest evaluator with less and less noise
        /// (0-ply), up to level 6. Each player meets its two nearest neighbours in duplicate pairs, where win rates stay
        /// measurable (level 6 wins every match against the weakest players).</item>
        /// <item>A Bradley-Terry fit over the whole chain gives each player a rating.</item>
        /// <item>Level 1 gets the noise rated 191 Elo above random play, which means winning 75% of matches against
        /// it. Levels 2..5 get the noise at even rating steps between level 1 and level 6, interpolated in the
        /// logarithm of the noise.</item>
        /// </list>
        /// Paste the result into BotLevels.
        /// </summary>
        public static void Calibrate(string[] args)
        {
            var pairs = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 300;
            var noises = new[] { 0.6, 0.45, 0.32, 0.22, 0.16, 0.11, 0.08, 0.055, 0.04, 0.028, 0.02, 0.014, 0.01, 0.007, 0.005, 0.0035, 0.0 };
            foreach (var variant in Variants(args[1]).Where(v => v != BackgammonVariant.Sreshta))
            {
                var clock = Stopwatch.StartNew();
                var players = new List<ArenaPlayer> { ArenaPlayer.Parse("random") };
                players.AddRange(noises.Select(ArenaPlayer.Noisy));
                players.Add(ArenaPlayer.Parse("L6"));
                var wins = new double[players.Count, players.Count];
                for (var i = 0; i < players.Count; i++)
                {
                    for (var j = i + 1; j <= Math.Min(i + 2, players.Count - 1); j++)
                    {
                        var stats = ArenaRunner.PlayPairs(variant, players[i], players[j], pairs, Environment.ProcessorCount, seedBase: 1 + (7919 * ((i * players.Count) + j)));
                        wins[i, j] = stats.WinsA;
                        wins[j, i] = stats.Matches - stats.WinsA;
                        Console.WriteLine($"    {variant} {stats}");
                    }
                }

                var ratings = BradleyTerry.Fit(wins);
                var random = ratings[0];
                var top = ratings[^1];
                Console.WriteLine($"  {variant} ratings, random = 0: " + string.Join(", ", players.Select((p, i) => $"{p.Name} {ratings[i] - random:F0}")));

                // Rating as a function of the noise: player i + 1 has noises[i], weakest first. Find the first pair
                // of neighbours whose ratings straddle the target and interpolate in the logarithm of the noise (a
                // noise of 0 counts as 0.002 there).
                double NoiseFor(double rating)
                {
                    if (rating <= ratings[1])
                    {
                        return noises[0];
                    }

                    for (var i = 0; i + 1 < noises.Length; i++)
                    {
                        double weaker = ratings[i + 1], stronger = ratings[i + 2];
                        if (rating >= Math.Min(weaker, stronger) && rating <= Math.Max(weaker, stronger))
                        {
                            var share = Math.Abs(stronger - weaker) < 1e-9 ? 0 : (rating - weaker) / (stronger - weaker);
                            var from = Math.Log(Math.Max(noises[i], 0.002));
                            var to = Math.Log(Math.Max(noises[i + 1], 0.002));
                            return Math.Exp(from + (share * (to - from)));
                        }
                    }

                    return 0;
                }

                var bottom = random + 191;
                var levels = new double[Backgammon.AI.BackgammonBot.Levels];
                for (var level = 1; level <= 5; level++)
                {
                    levels[level - 1] = NoiseFor(bottom + ((top - bottom) * (level - 1) / 5));
                }

                Console.WriteLine($"{variant} [{clock.Elapsed.TotalMinutes:F1} min]: random 0, level 1 at {bottom - random:F0}, level 6 at {top - random:F0}, a step of {(top - bottom) / 5:F0} Elo; noise by level: {{ {string.Join(", ", levels.Select(n => n.ToString("0.####", CultureInfo.InvariantCulture)))} }}");
            }
        }

        /// <summary>
        /// <c>timing &lt;variant|all&gt; [matches] [player]</c>: single-threaded decision times of a player (L6 by default)
        /// in self-play, with percentiles, plus the length of a match in decisions.
        /// </summary>
        public static void Timing(string[] args)
        {
            var matches = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 50;
            var player = ArenaPlayer.Parse(args.Length > 3 ? args[3] : "L6");
            foreach (var variant in Variants(args[1]))
            {
                // Warm up the JIT and the caches first.
                ArenaRunner.Play(variant, player, player, -1, -1, timeDecisions: false);
                var times = new List<double>();
                var decisions = 0L;
                var games = 0L;
                for (var seed = 1; seed <= matches; seed++)
                {
                    var stats = ArenaRunner.Play(variant, player, player, seed, seed, timeDecisions: true);
                    times.AddRange(stats.DecisionMicroseconds[0]);
                    times.AddRange(stats.DecisionMicroseconds[1]);
                    decisions += stats.Decisions[0] + stats.Decisions[1];
                    games += stats.Games;
                }

                times.Sort();
                double Percentile(double p) => times[Math.Min(times.Count - 1, (int)(p * times.Count))] / 1000;
                Console.WriteLine(
                    $"{variant,-10} {player.Name}: {times.Count} decisions, ms p50 {Percentile(0.5):F2} p90 {Percentile(0.9):F2} p99 {Percentile(0.99):F2} " +
                    $"max {times[^1] / 1000:F2} mean {times.Average() / 1000:F2}; {(double)decisions / matches / 2:F1} decisions per player and " +
                    $"{(double)games / matches:F2} games a match");
            }
        }

        /// <summary>
        /// <c>bench [positions]</c>: the engine's speed on one thread. For each version it times the move generator over
        /// sampled positions and all 21 rolls, the evaluators, and whole random games through <see cref="BackgammonMatch"/>.
        /// </summary>
        public static void Bench(string[] args)
        {
            var count = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 2_000;
            foreach (var version in Enum.GetValues<BackgammonVersion>())
            {
                var samples = VectorWriter.SamplePositions(version, count, seed: 77);
                var generator = new Backgammon.Logic.Rules.StageGenerator();
                var ends = new List<Backgammon.Logic.Rules.StageEnd>();
                var clock = Stopwatch.StartNew();
                long stages = 0, total = 0;
                double doubles = 0;
                for (var round = 0; round < 3; round++)
                {
                    foreach (var (position, seat) in samples)
                    {
                        for (var high = 1; high <= 6; high++)
                        {
                            for (var low = 1; low <= high; low++)
                            {
                                var started = Stopwatch.GetTimestamp();
                                generator.Generate(position, seat, high == low ? Backgammon.Logic.Rules.StageDice.Same(high, 4) : Backgammon.Logic.Rules.StageDice.Distinct(high, low), ends);
                                if (high == low)
                                {
                                    doubles += Stopwatch.GetElapsedTime(started).TotalMicroseconds;
                                }

                                stages++;
                                total += ends.Count;
                            }
                        }
                    }
                }

                var generation = clock.Elapsed.TotalMicroseconds / stages;
                var baseline = Backgammon.AI.Evaluation.BaselineEvaluator.Instance;
                var network = Backgammon.AI.Neural.NeuralNetwork.CreateRandom(version, Backgammon.AI.Neural.FeatureEncoder.Inputs, 128, 1);
                var neural = new Backgammon.AI.Neural.NeuralEvaluator(network);
                var latest = Backgammon.AI.Neural.FeatureEncoder.LatestLayout;
                var network2 = Backgammon.AI.Neural.NeuralNetwork.CreateRandom(version, Backgammon.AI.Neural.FeatureEncoder.InputsOf(latest), 128, 1, latest);
                var neural2 = new Backgammon.AI.Neural.NeuralEvaluator(network2);
                var rolls = new Backgammon.AI.Evaluation.RollCounts(5, 5);
                double Time(Backgammon.AI.Evaluation.IEvaluator evaluator)
                {
                    var watch = Stopwatch.StartNew();
                    var sum = 0.0;
                    for (var round = 0; round < 20; round++)
                    {
                        foreach (var (position, seat) in samples)
                        {
                            sum += evaluator.Evaluate(position, seat, rolls).Win;
                        }
                    }

                    return sum >= 0 ? watch.Elapsed.TotalMicroseconds / (20.0 * samples.Count) : 0;
                }

                double Features()
                {
                    var watch = Stopwatch.StartNew();
                    var sum = 0;
                    for (var round = 0; round < 20; round++)
                    {
                        foreach (var (position, seat) in samples)
                        {
                            sum += Backgammon.AI.Neural.FeatureEncoder.HittingRolls(position, seat) + Backgammon.AI.Neural.FeatureEncoder.HittingRolls(position, 1 - seat);
                            sum += Backgammon.AI.Neural.FeatureEncoder.LongestBlock(position, seat) + Backgammon.AI.Neural.FeatureEncoder.LongestBlock(position, 1 - seat);
                        }
                    }

                    return sum >= 0 ? watch.Elapsed.TotalMicroseconds / (20.0 * samples.Count) : 0;
                }

                Time(baseline);
                Time(neural);
                Time(neural2);
                Features();
                Console.WriteLine(
                    $"{version,-10} generation {generation:F1} us a stage (doubles {doubles / (stages * 6.0 / 21):F1} us), {(double)total / stages:F1} ends; " +
                    $"evaluation: baseline {Time(baseline):F2} us, network {Time(neural):F2} us, layout {latest} {Time(neural2):F2} us (its extra inputs {Features():F2} us)");
            }
        }

        /// <summary>
        /// When a ladder has all six levels of a single version, prints the noises that would put levels 2..5 at even
        /// rating steps between its own level 1 and level 6, interpolated in the logarithm of the noise between the
        /// levels' measured ratings (a noise of 0 counts as 0.002).
        /// </summary>
        private static void SuggestNoises(BackgammonVariant variant, string[] names, double[] ratings)
        {
            var levels = Enumerable.Range(1, AI.BackgammonBot.Levels).Select(level => Array.IndexOf(names, "L" + level)).ToArray();
            if (variant == BackgammonVariant.Sreshta || levels.Any(index => index < 0))
            {
                return;
            }

            var version = (BackgammonVersion)(int)variant;
            var noise = Enumerable.Range(1, AI.BackgammonBot.Levels).Select(level => Math.Log(Math.Max(AI.BotLevels.SettingsFor(level, version).Noise, 0.002))).ToArray();
            var rating = levels.Select(index => ratings[index]).ToArray();
            var suggested = new double[AI.BackgammonBot.Levels - 1];
            suggested[0] = Math.Exp(noise[0]);
            for (var level = 2; level < AI.BackgammonBot.Levels; level++)
            {
                var target = rating[0] + ((rating[^1] - rating[0]) * (level - 1) / (AI.BackgammonBot.Levels - 1));
                var j = 0;
                while (j < rating.Length - 2 && rating[j + 1] < target)
                {
                    j++;
                }

                var span = rating[j + 1] - rating[j];
                var share = Math.Abs(span) < 1e-9 ? 0 : Math.Clamp((target - rating[j]) / span, 0, 1);
                suggested[level - 1] = Math.Exp(noise[j] + (share * (noise[j + 1] - noise[j])));
            }

            Console.WriteLine($"  even steps of {(rating[^1] - rating[0]) / (AI.BackgammonBot.Levels - 1):F0} Elo would take noises {{ {string.Join(", ", suggested.Select(n => n.ToString("0.####", CultureInfo.InvariantCulture)))}, 0 }}");
        }
    }
}
