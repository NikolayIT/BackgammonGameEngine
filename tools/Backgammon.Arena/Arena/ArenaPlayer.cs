namespace Backgammon.Arena
{
    using System;

    using Backgammon.AI;
    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Search;
    using Backgammon.Logic;

    /// <summary>A player in the arena: a named way to choose an action from a view.</summary>
    internal abstract class ArenaPlayer
    {
        protected ArenaPlayer(string name)
        {
            this.Name = name;
        }

        public string Name { get; }

        /// <summary>
        /// Makes a player from its name:
        /// <list type="bullet">
        /// <item><c>L1</c>..<c>L6</c>: the shipped levels.</item>
        /// <item><c>random</c>: a uniformly random legal play.</item>
        /// <item><c>baseline</c>: the hand-written evaluator with level 6's search (look-ahead and one roll).</item>
        /// <item><c>baseline0</c>: the same without the look-ahead.</item>
        /// <item><c>net:&lt;file&gt;</c>: a network from a file, with level 6's search.</item>
        /// <item><c>net1:&lt;file&gt;</c>: the same, looking one roll deeper at its two best plays.</item>
        /// <item><c>noise:&lt;σ&gt;</c>: the strongest evaluator with that much noise and no look-ahead.</item>
        /// <item><c>search:&lt;look-ahead&gt;:&lt;one roll&gt;:&lt;budget&gt;</c>: the strongest evaluator with those search settings.</item>
        /// </list>
        /// </summary>
        public static ArenaPlayer Parse(string name) => name switch
        {
            "random" => new RandomPlayer(),
            "baseline" => new EvaluatorPlayer(name, _ => BaselineEvaluator.Instance, new SearchSettings(0, 3, 4_000, 2)),
            "baseline0" => new EvaluatorPlayer(name, _ => BaselineEvaluator.Instance, new SearchSettings(0, 0, 0)),
            _ when name.Length == 2 && name[0] == 'L' && char.IsDigit(name[1]) => new LevelPlayer(name[1] - '0'),
            _ when name.StartsWith("net:", StringComparison.Ordinal) => NetworkPlayer(name, name[4..], 0),
            _ when name.StartsWith("net1:", StringComparison.Ordinal) => NetworkPlayer(name, name[5..], 2),
            _ when name.StartsWith("search:", StringComparison.Ordinal) => Searching(name),
            _ when name.StartsWith("noise:", StringComparison.Ordinal) => Noisy(double.Parse(name[6..], System.Globalization.CultureInfo.InvariantCulture)),
            _ => throw new ArgumentException($"Unknown player '{name}'."),
        };

        /// <summary>The strongest evaluator of each version (its shipped network, or the baseline) with noise, no look-ahead.</summary>
        public static ArenaPlayer Noisy(double noise) =>
            new EvaluatorPlayer($"noise:{noise:0.####}", BotLevels.EvaluatorFor, new SearchSettings(noise, 0, 0));

        public abstract BackgammonAction Choose(BackgammonSeatView view, Random random);

        private static ArenaPlayer Searching(string name)
        {
            var parts = name.Split(':');
            var settings = new SearchSettings(0, int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
            return new EvaluatorPlayer(name, BotLevels.EvaluatorFor, settings);
        }

        private static ArenaPlayer NetworkPlayer(string name, string path, int oneRoll)
        {
            using var stream = System.IO.File.OpenRead(path);
            var evaluator = new Backgammon.AI.Neural.NeuralEvaluator(Backgammon.AI.Neural.NeuralNetwork.Read(stream));
            return new EvaluatorPlayer(name, _ => evaluator, new SearchSettings(0, 3, 4_000, oneRoll));
        }

        private sealed class LevelPlayer : ArenaPlayer
        {
            private readonly int level;

            public LevelPlayer(int level)
                : base("L" + level)
            {
                this.level = level;
            }

            public override BackgammonAction Choose(BackgammonSeatView view, Random random) => BackgammonBot.Choose(view, this.level, random);
        }

        private sealed class RandomPlayer : ArenaPlayer
        {
            public RandomPlayer()
                : base("random")
            {
            }

            public override BackgammonAction Choose(BackgammonSeatView view, Random random)
            {
                var moves = BackgammonStageMoves.For(view);
                return new BackgammonAction { Steps = moves.Outcomes[random.Next(moves.OutcomeCount)].Steps };
            }
        }
    }
}
