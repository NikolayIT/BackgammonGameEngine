namespace Backgammon.Trainer
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Neural;
    using Backgammon.AI.Search;
    using Backgammon.Arena;
    using Backgammon.Logic;

    /// <summary>
    /// The training loop. Games are played in batches of fixed slots, every game of a batch with the same frozen
    /// network and a seed made from (seed, batch, slot). Each slot adds its changes to its own buffer; the buffers are
    /// then summed in slot order and applied. The result depends only on the settings: not on the number of threads,
    /// and not on timing.
    /// </summary>
    internal static class Training
    {
        public static void Train(TrainingSettings settings)
        {
            var network = settings.Init != null
                ? Load(settings.Init).Copy("init " + Path.GetFileName(settings.Init))
                : NeuralNetwork.CreateRandom(settings.Version, FeatureEncoder.Inputs, settings.Hidden, settings.Seed);
            var parameters = network.ParameterCount;
            var buffers = new float[settings.Batch][];
            var learners = new TdLearner[settings.Batch];
            for (var slot = 0; slot < settings.Batch; slot++)
            {
                buffers[slot] = new float[parameters];
                learners[slot] = new TdLearner(network);
            }

            var total = new float[parameters];
            var log = Path.ChangeExtension(settings.Out, ".log.csv");
            File.WriteAllText(log, $"# {settings}\ngames,seconds,alpha,error,winRate,winRateError\n");
            Console.WriteLine($"Training {settings} ({parameters} parameters)");

            var clock = Stopwatch.StartNew();
            var games = 0L;
            var nextEval = settings.EvalEvery;
            var best = -1.0;
            var error = 0.0;
            var states = 0L;
            for (var batch = 0; games < settings.Games; batch++)
            {
                var progress = (double)games / settings.Games;
                var alpha = (float)(settings.Alpha * Math.Pow(settings.AlphaEnd / settings.Alpha, progress));
                var played = new int[settings.Batch];
                var errors = new double[settings.Batch];
                var counts = new int[settings.Batch];
                var evaluator = new NeuralEvaluator(network);
                Parallel.For(0, settings.Batch, new ParallelOptions { MaxDegreeOfParallelism = settings.Threads }, slot =>
                {
                    Array.Clear(buffers[slot]);
                    var seed = Seed(settings.Seed, batch, slot);
                    var exploration = settings.Exploration * (1 - progress);
                    foreach (var game in new SelfPlay(evaluator).PlayMatch(settings.Version, seed, exploration))
                    {
                        errors[slot] += learners[slot].Accumulate(game, alpha, (float)settings.Lambda, buffers[slot]);
                        counts[slot] += game.States.Count;
                        played[slot]++;
                    }
                });

                // Sum the slots in order, in parallel over chunks of the parameters, and apply.
                Parallel.For(0, (parameters + 4095) / 4096, new ParallelOptions { MaxDegreeOfParallelism = settings.Threads }, chunk =>
                {
                    var from = chunk * 4096;
                    var to = Math.Min(parameters, from + 4096);
                    for (var i = from; i < to; i++)
                    {
                        var sum = 0f;
                        for (var slot = 0; slot < settings.Batch; slot++)
                        {
                            sum += buffers[slot][i];
                        }

                        total[i] = sum;
                    }
                });

                Apply(network, total);
                for (var slot = 0; slot < settings.Batch; slot++)
                {
                    games += played[slot];
                    error += errors[slot];
                    states += counts[slot];
                }

                if (games >= nextEval || games >= settings.Games)
                {
                    nextEval += settings.EvalEvery;
                    var (winRate, winError) = Measure(network, settings, settings.EvalPairs);
                    var line = $"{games},{clock.Elapsed.TotalSeconds:F0},{alpha:F5},{error / Math.Max(1, states):F5},{winRate:F4},{winError:F4}";
                    File.AppendAllText(log, line + "\n");
                    Console.WriteLine($"{games,9} games {clock.Elapsed.TotalMinutes,6:F1} min alpha {alpha:F4} error {error / Math.Max(1, states):F4} vs baseline {winRate:P1} ± {1.96 * winError:P1}");
                    error = 0;
                    states = 0;
                    Save(network, settings, games, Path.ChangeExtension(settings.Out, $".{games / 1000}k.bin"));
                    if (winRate > best)
                    {
                        best = winRate;
                        Save(network, settings, games, Path.ChangeExtension(settings.Out, ".best.bin"));
                    }
                }
            }

            Save(network, settings, games, settings.Out);
            Console.WriteLine($"Done: {games} games in {clock.Elapsed.TotalMinutes:F1} min, best {best:P1} against the baseline.");
        }

        /// <summary>
        /// A seed made from three numbers, the same in every process. HashCode.Combine is not, because .NET seeds it
        /// randomly per process.
        /// </summary>
        public static int Seed(int a, int b, int c)
        {
            var x = ((ulong)(uint)a * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)b * 0xC2B2AE3D27D4EB4FUL) ^ ((ulong)(uint)c * 0x165667B19E3779F9UL);
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return (int)((x ^ (x >> 31)) & 0x7FFFFFFF);
        }

        /// <summary>
        /// The network against the baseline in duplicate pairs of matches, both with level 6's search (the look-ahead
        /// through known stages and one roll deeper at the two best plays).
        /// </summary>
        public static (double WinRate, double Error) Measure(NeuralNetwork network, TrainingSettings settings, int pairs)
        {
            var search = new SearchSettings(0, 3, 4_000, 2);
            var candidate = new EvaluatorPlayer("net", _ => new NeuralEvaluator(network), search);
            var baseline = new EvaluatorPlayer("baseline", _ => BaselineEvaluator.Instance, search);
            var stats = ArenaRunner.PlayPairs((BackgammonVariant)(int)settings.Version, candidate, baseline, pairs, settings.Threads, seedBase: 1_000_000);
            return (stats.WinRate, stats.Error);
        }

        public static NeuralNetwork Load(string path)
        {
            using var stream = File.OpenRead(path);
            return NeuralNetwork.Read(stream);
        }

        private static void Apply(NeuralNetwork network, float[] total)
        {
            var offset = 0;
            foreach (var part in new[] { network.W1, network.B1, network.W2, network.B2 })
            {
                for (var i = 0; i < part.Length; i++)
                {
                    part[i] += total[offset + i];
                }

                offset += part.Length;
            }
        }

        private static void Save(NeuralNetwork network, TrainingSettings settings, long games, string path)
        {
            var copy = network.Copy($"TD(lambda) self-play: {settings}; {games} games");
            using var stream = File.Create(path);
            copy.Write(stream);
        }
    }
}
