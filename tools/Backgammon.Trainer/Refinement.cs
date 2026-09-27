namespace Backgammon.Trainer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Neural;
    using Backgammon.AI.Search;
    using Backgammon.Arena;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// Refines a trained network towards its own one-roll look-ahead, round by round:
    /// <list type="number">
    /// <item>Positions are collected from self-play with the network, at the start of each roll.</item>
    /// <item>Each position's target is the network's value one roll deeper: for each of the 21 rolls the roller's best
    /// play by expected points (an escalating double played stage by stage), valued by the network with the opponent
    /// to roll, averaged by the rolls' chances. A play that ends the game counts exactly.</item>
    /// <item>The network is trained towards those targets by plain SGD on the cross-entropy, one position at a time,
    /// in a shuffled order.</item>
    /// </list>
    /// Every step depends only on the settings, not on the threads: positions come from fixed slots with fixed seeds,
    /// each target is computed on its own, and the training runs on one thread.
    /// </summary>
    internal static class Refinement
    {
        private const int Slots = 64;

        public static void Refine(TrainingSettings settings)
        {
            var init = settings.Init ?? throw new ArgumentException("refine needs --init <network file>.");
            var reference = Training.Load(init).Copy("reference", settings.Version);
            var network = reference.Copy($"refined from {Path.GetFileName(init)}");
            var clock = Stopwatch.StartNew();
            var holdout = Collect(network, settings, Math.Max(10_000, settings.Positions / 50), seedBase: 1_000_000);
            Console.WriteLine($"Refining {Path.GetFileName(init)}: {settings}; {settings.Positions} positions a round, {settings.Rounds} rounds, {settings.Epochs} epochs");
            Console.WriteLine($"  hold-out error against one roll deeper: {HoldoutError(network, holdout):F5}");
            for (var round = 1; round <= settings.Rounds; round++)
            {
                var states = Collect(network, settings, settings.Positions, seedBase: round);
                var targets = Targets(network, states, settings.Threads);
                var error = Train(network, states, targets, settings, round);
                var path = Path.ChangeExtension(settings.Out, $".r{round}.bin");
                Save(network, settings, init, round, path);
                Console.WriteLine($"round {round} [{clock.Elapsed.TotalMinutes:F1} min]: training error {error:F5}, hold-out error {HoldoutError(network, holdout):F5}; {Compare(network, reference, settings)}");
            }

            Save(network, settings, init, settings.Rounds, settings.Out);
        }

        /// <summary>The network's value one roll deeper, as its three outputs for the roller.</summary>
        public static void OnePly(NeuralEvaluator evaluator, StageGenerator generator, List<StageEnd> ends, in TrainingState state, Span<float> target)
        {
            var roller = state.Roller;
            var after = state.Rolls.After(roller);
            var escalates = state.Rolls.NextEscalates(state.Position, roller);
            double win = 0, winDouble = 0, loseDouble = 0;
            for (var high = 1; high <= 6; high++)
            {
                for (var low = 1; low <= high; low++)
                {
                    var weight = (high == low ? 1.0 : 2.0) / 36;
                    Outcome chosen;
                    if (high != low || !escalates)
                    {
                        chosen = Best(evaluator, generator, ends, state.Position, roller, high == low ? StageDice.Same(high, 4) : StageDice.Distinct(high, low), after, out _, out _, out _, out _);
                    }
                    else
                    {
                        // An escalating double, stage by stage. When the chain breaks after the roller has played
                        // some of it, the opponent plays the rest before it rolls; a chain the roller could not start
                        // is lost.
                        var current = state.Position;
                        var played = 0;
                        chosen = default;
                        for (var die = high; die <= 6; die++)
                        {
                            chosen = Best(evaluator, generator, ends, current, roller, StageDice.Same(die, 4), after, out var next, out var complete, out var stagePlayed, out var ended);
                            if (!complete)
                            {
                                if (!ended && played + stagePlayed > 0)
                                {
                                    chosen = Remainder(evaluator, generator, ends, next, 1 - roller, die, 4 - stagePlayed, after);
                                }

                                break;
                            }

                            played += 4;
                            current = next;
                        }
                    }

                    win += weight * (chosen.Win + (0.5 * chosen.Draw));
                    winDouble += weight * chosen.WinDouble;
                    loseDouble += weight * chosen.LoseDouble;
                }
            }

            target[0] = (float)win;
            target[1] = (float)winDouble;
            target[2] = (float)loseDouble;
        }

        /// <summary>
        /// The mover's best play of a stage by expected points, valued with <paramref name="onRollAfter"/> to roll next;
        /// the outcome is for the mover.
        /// </summary>
        private static Outcome Best(NeuralEvaluator evaluator, StageGenerator generator, List<StageEnd> ends, in Position position, int mover, int onRollAfter, StageDice dice, RollCounts after, out Position chosen, out int playable, out bool ended)
        {
            playable = generator.Generate(position, mover, dice, ends);
            var best = 0;
            var bestPoints = double.NegativeInfinity;
            Outcome bestOutcome = default;
            for (var i = 0; i < ends.Count; i++)
            {
                var end = ends[i];
                Outcome outcome;
                if (end.End != GameEnd.None)
                {
                    outcome = end.End switch
                    {
                        GameEnd.BorneOff => Outcome.Won(end.Position.Count(1 - mover, Geometry.Off) == 0),
                        GameEnd.Mother => Outcome.Won(doubled: true),
                        _ => Outcome.Drawn,
                    };
                }
                else
                {
                    var value = evaluator.Evaluate(end.Position, onRollAfter, after);
                    outcome = onRollAfter == mover ? value : value.Flip();
                }

                var points = outcome.Points();
                if (points > bestPoints)
                {
                    bestPoints = points;
                    bestOutcome = outcome;
                    best = i;
                }
            }

            chosen = ends[best].Position;
            ended = ends[best].End != GameEnd.None;
            return bestOutcome;
        }

        private static Outcome Best(NeuralEvaluator evaluator, StageGenerator generator, List<StageEnd> ends, in Position position, int roller, StageDice dice, RollCounts after, out Position chosen, out bool complete, out int playable, out bool ended)
        {
            var outcome = Best(evaluator, generator, ends, position, roller, 1 - roller, dice, after, out chosen, out playable, out ended);
            complete = playable == dice.Count && !ended;
            return outcome;
        }

        /// <summary>
        /// The opponent plays the rest of the roller's chain, stage by stage and greedily for itself, then rolls:
        /// <paramref name="left"/> dice of <paramref name="die"/>, then four of each higher die. Returns the roller's outcome.
        /// </summary>
        private static Outcome Remainder(NeuralEvaluator evaluator, StageGenerator generator, List<StageEnd> ends, in Position position, int opponent, int die, int left, RollCounts after)
        {
            var current = position;
            for (var stage = die; stage <= 6; stage++)
            {
                var dice = StageDice.Same(stage, stage == die ? left : 4);
                var outcome = Best(evaluator, generator, ends, current, opponent, opponent, dice, after, out var next, out _, out var ended);
                if (ended)
                {
                    return outcome.Flip();
                }

                current = next;
            }

            return evaluator.Evaluate(current, opponent, after).Flip();
        }

        private static TrainingState[] Collect(NeuralNetwork network, TrainingSettings settings, long count, int seedBase)
        {
            var evaluator = new NeuralEvaluator(network);
            var perSlot = new List<TrainingState>[Slots];
            var quota = (count + Slots - 1) / Slots;
            Parallel.For(0, Slots, new ParallelOptions { MaxDegreeOfParallelism = settings.Threads }, slot =>
            {
                var states = new List<TrainingState>();
                var play = new SelfPlay(evaluator);
                for (var game = 0; states.Count < quota; game++)
                {
                    foreach (var record in play.PlayMatch(settings.Version, Training.Seed(settings.Seed + (7919 * seedBase), slot, game), settings.Exploration))
                    {
                        states.AddRange(record.States);
                    }
                }

                perSlot[slot] = states.GetRange(0, (int)quota);
            });

            return perSlot.SelectMany(states => states).Take((int)count).ToArray();
        }

        private static float[] Targets(NeuralNetwork network, TrainingState[] states, int threads)
        {
            var evaluator = new NeuralEvaluator(network);
            var targets = new float[states.Length * NeuralNetwork.Outputs];
            Parallel.For(
                0,
                (states.Length + 1023) / 1024,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                () => (new StageGenerator(), new List<StageEnd>()),
                (chunk, _, local) =>
                {
                    for (var i = chunk * 1024; i < Math.Min(states.Length, (chunk + 1) * 1024); i++)
                    {
                        OnePly(evaluator, local.Item1, local.Item2, states[i], targets.AsSpan(i * NeuralNetwork.Outputs, NeuralNetwork.Outputs));
                    }

                    return local;
                },
                _ => { });
            return targets;
        }

        private static double Train(NeuralNetwork network, TrainingState[] states, float[] targets, TrainingSettings settings, int round)
        {
            var learner = new SupervisedLearner(network);
            var order = Enumerable.Range(0, states.Length).ToArray();
            var steps = (double)settings.Epochs * states.Length;
            var step = 0L;
            var error = 0.0;
            for (var epoch = 0; epoch < settings.Epochs; epoch++)
            {
                var random = new Random(Training.Seed(settings.Seed, round, epoch));
                random.Shuffle(order);
                error = 0;
                foreach (var i in order)
                {
                    var alpha = (float)(settings.Alpha * Math.Pow(settings.AlphaEnd / settings.Alpha, step++ / steps));
                    error += learner.Step(states[i], targets.AsSpan(i * NeuralNetwork.Outputs, NeuralNetwork.Outputs), alpha);
                }
            }

            return error / states.Length;
        }

        private static double HoldoutError(NeuralNetwork network, TrainingState[] holdout)
        {
            var targets = Targets(network, holdout, Environment.ProcessorCount);
            var evaluator = new NeuralEvaluator(network);
            var error = 0.0;
            for (var i = 0; i < holdout.Length; i++)
            {
                var outcome = evaluator.Evaluate(holdout[i].Position, holdout[i].Roller, holdout[i].Rolls);
                var t = targets.AsSpan(i * NeuralNetwork.Outputs, NeuralNetwork.Outputs);
                error += Square(outcome.Win - t[0]) + Square(outcome.WinDouble - t[1]) + Square(outcome.LoseDouble - t[2]);
            }

            return error / holdout.Length;

            static double Square(double x) => x * x;
        }

        private static string Compare(NeuralNetwork candidate, NeuralNetwork reference, TrainingSettings settings)
        {
            var variant = (BackgammonVariant)(int)settings.Version;
            var results = new List<string>();
            foreach (var (name, search) in new[] { ("0-ply", new SearchSettings(0, 0, 0)), ("level 6", new SearchSettings(0, 3, 3_000, 2)) })
            {
                var a = new EvaluatorPlayer("refined", _ => new NeuralEvaluator(candidate), search);
                var b = new EvaluatorPlayer("reference", _ => new NeuralEvaluator(reference), search);
                var stats = ArenaRunner.PlayPairs(variant, a, b, settings.EvalPairs, settings.Threads, seedBase: 2_000_000);
                results.Add($"{name} against the start {stats.WinRate:P1} ± {1.96 * stats.Error:P1}");
            }

            return string.Join(", ", results);
        }

        private static void Save(NeuralNetwork network, TrainingSettings settings, string init, int round, string path)
        {
            var copy = network.Copy($"refined from {Path.GetFileName(init)} towards one roll deeper: {settings}; {settings.Positions} positions x {round} rounds, {settings.Epochs} epochs");
            using var stream = File.Create(path);
            copy.Write(stream);
        }
    }
}
