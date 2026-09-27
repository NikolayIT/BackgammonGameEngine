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
