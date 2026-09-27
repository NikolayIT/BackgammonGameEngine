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
        /// <item><c>baseline</c>: the hand-written evaluator with level 6's search.</item>
        /// <item><c>baseline0</c>: the same without the look-ahead.</item>
        /// </list>
        /// </summary>
        public static ArenaPlayer Parse(string name) => name switch
        {
            "random" => new RandomPlayer(),
            "baseline" => new EvaluatorPlayer(name, _ => BaselineEvaluator.Instance, new SearchSettings(0, 3, 4_000)),
            "baseline0" => new EvaluatorPlayer(name, _ => BaselineEvaluator.Instance, new SearchSettings(0, 0, 0)),
            _ when name.Length == 2 && name[0] == 'L' && char.IsDigit(name[1]) => new LevelPlayer(name[1] - '0'),
            _ => throw new ArgumentException($"Unknown player '{name}'."),
        };

        public abstract BackgammonAction Choose(BackgammonSeatView view, Random random);

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
