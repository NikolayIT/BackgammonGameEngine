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
            }
        }

        /// <summary>
        /// <c>calibrate &lt;variant|all&gt; [pairs]</c>: finds the noise of each level of a version. Level 1 gets the
        /// largest noise that still beats a random player in 75% of matches; levels 2..5 get the noise that puts them
        /// at even rating steps between level 1 and level 6 (as measured against level 6). Paste the result into
        /// BotLevels.
        /// </summary>
        public static void Calibrate(string[] args)
        {
            var pairs = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 300;
            var strong = ArenaPlayer.Parse("L6");
            var random = ArenaPlayer.Parse("random");
            foreach (var variant in Variants(args[1]).Where(v => v != BackgammonVariant.Sreshta))
            {
                var clock = Stopwatch.StartNew();
                double Against(double noise, ArenaPlayer opponent, bool elo)
                {
                    var stats = ArenaRunner.PlayPairs(variant, ArenaPlayer.Noisy(noise), opponent, pairs, Environment.ProcessorCount);
                    Console.WriteLine($"    {variant} {stats}");
                    return elo ? stats.Elo : stats.WinRate;
                }

                var noises = new double[Backgammon.AI.BackgammonBot.Levels];
                noises[0] = LargestNoise(0.002, 1.0, noise => Against(noise, random, elo: false) >= 0.75);
                var bottom = Against(noises[0], strong, elo: true);
                for (var level = 2; level <= 5; level++)
                {
                    var target = bottom * (6 - level) / 5;
                    noises[level - 1] = LargestNoise(0.0005, noises[level - 2], noise => Against(noise, strong, elo: true) >= target);
                }

                Console.WriteLine($"{variant} [{clock.Elapsed.TotalMinutes:F1} min]: level 1 is {bottom:F0} Elo below level 6; noise by level: {{ {string.Join(", ", noises.Select(n => n.ToString("0.####", CultureInfo.InvariantCulture)))} }}");
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

                Time(baseline);
                Time(neural);
                Console.WriteLine(
                    $"{version,-10} generation {generation:F1} us a stage (doubles {doubles / (stages * 6.0 / 21):F1} us), {(double)total / stages:F1} ends; " +
                    $"evaluation: baseline {Time(baseline):F2} us, network {Time(neural):F2} us");
            }
        }

        /// <summary>The largest noise in [low, high] for which <paramref name="holds"/> is true, by bisection on its logarithm.</summary>
        private static double LargestNoise(double low, double high, Func<double, bool> holds)
        {
            if (holds(high))
            {
                return high;
            }

            for (var step = 0; step < 7; step++)
            {
                var middle = Math.Sqrt(low * high);
                if (holds(middle))
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }
}
