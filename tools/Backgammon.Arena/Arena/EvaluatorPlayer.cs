namespace Backgammon.Arena
{
    using System;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Search;
    using Backgammon.Logic;

    /// <summary>An arena player built from any evaluator and search settings, for experiments.</summary>
    internal sealed class EvaluatorPlayer : ArenaPlayer
    {
        private readonly Func<BackgammonVersion, IEvaluator> evaluator;
        private readonly SearchSettings settings;

        public EvaluatorPlayer(string name, Func<BackgammonVersion, IEvaluator> evaluator, SearchSettings settings)
            : base(name)
        {
            this.evaluator = evaluator;
            this.settings = settings;
        }

        public override BackgammonAction Choose(BackgammonSeatView view, Random random)
        {
            var situation = Situation.FromView(view);
            var (ends, choice) = Chooser.Current.Choose(situation, this.evaluator(situation.Position.Version), this.settings, random);
            return new BackgammonAction { Steps = ends[choice].Steps.ToSteps() };
        }
    }
}
