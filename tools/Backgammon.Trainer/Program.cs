namespace Backgammon.Trainer
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;

    using Backgammon.AI.Neural;
    using Backgammon.Logic;

    /// <summary>
    /// Trains the bots' networks by TD(λ) self-play. Run it in Release from a copy of the binaries (a running trainer
    /// locks bin), with <c>DOTNET_EnableAVX512F=0</c> so every machine does the same arithmetic.
    /// <list type="bullet">
    /// <item><c>train --version tapa --seed 1 --games 1000000 --out tapa.bin</c> trains a network and writes
    /// checkpoints, the best one against the baseline, and a log.</item>
    /// <item><c>refine --version tapa --init tapa.bin --positions 1000000 --rounds 8 --alpha 0.0003 --alpha-end 0.00003
    /// --out tapa-r.bin</c> trains a network towards its own one-roll look-ahead (see Refinement).</item>
    /// <item><c>validate --version tapa --weights tapa.bin --eval-pairs 2000</c> measures a network against the
    /// baseline (the ship gate).</item>
    /// <item><c>stats --version tapa --weights tapa.bin --games 20000</c> gives the share of 2-point results and draws
    /// in self-play, for the match equity table.</item>
    /// <item><c>bench --version tapa --games 2000</c> gives the self-play speed.</item>
    /// </list>
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            NativeMethods.DisablePowerThrottling();
            var command = args.Length > 0 ? args[0] : "help";
            var settings = TrainingSettings.Parse(args, 1);
            switch (command)
            {
                case "train":
                    Training.Train(settings);
                    return 0;

                case "refine":
                    Refinement.Refine(settings);
                    return 0;

                case "validate":
                    var network = Training.Load(settings.Weights ?? settings.Out);
                    var (winRate, error) = Training.Measure(network, settings, settings.EvalPairs);
                    Console.WriteLine($"{settings.Version}: network against the baseline {winRate:P2} ± {1.96 * error:P2} over {settings.EvalPairs * 2} matches");
                    Console.WriteLine(network.Description);
                    return 0;

                case "stats":
                    Stats(settings);
                    return 0;

                case "bench":
                    Bench(settings);
                    return 0;

                default:
                    Console.WriteLine("Commands: train, validate, stats, bench (see Program.cs); settings as --name value (TrainingSettings.cs).");
                    return command == "help" ? 0 : 1;
            }
        }

        private static void Stats(TrainingSettings settings)
        {
            var network = Training.Load(settings.Weights ?? settings.Out);
            var kinds = new long[5];
            var states = 0L;
            var gate = new object();
            System.Threading.Tasks.Parallel.For(0, (int)settings.Games, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = settings.Threads }, seed =>
            {
                var games = new SelfPlay(new NeuralEvaluator(network)).PlayMatch(settings.Version, seed + 7_000_000, 0);
                lock (gate)
                {
                    foreach (var game in games)
                    {
                        kinds[(int)game.Result!.Kind]++;
                        states += game.States.Count;
                    }
                }
            });

            var total = kinds.Sum();
            var decisive = kinds[0] + kinds[1] + kinds[2];
            Console.WriteLine(
                $"{settings.Version}: {total} games, 2-point share of decisive {(double)(kinds[1] + kinds[2]) / decisive:P1} " +
                $"(марс {(double)kinds[1] / decisive:P1}, майка {(double)kinds[2] / decisive:P1}), draws {(double)kinds[3] / total:P2}, " +
                $"stuck {kinds[4]}, {(double)states / total:F1} rolls a game");
        }

        private static void Bench(TrainingSettings settings)
        {
            var network = NeuralNetwork.CreateRandom(settings.Version, FeatureEncoder.Inputs, settings.Hidden, settings.Seed);
            var play = new SelfPlay(new NeuralEvaluator(network));
            play.PlayMatch(settings.Version, 0, 0);
            var clock = Stopwatch.StartNew();
            var states = 0L;
            for (var seed = 1; seed <= settings.Games; seed++)
            {
                foreach (var game in play.PlayMatch(settings.Version, seed, 0))
                {
                    states += game.States.Count;
                }
            }

            Console.WriteLine($"{settings.Version}: {settings.Games / clock.Elapsed.TotalSeconds:F0} games a second on one thread, {(double)states / settings.Games:F0} rolls a game");
        }
    }
}
