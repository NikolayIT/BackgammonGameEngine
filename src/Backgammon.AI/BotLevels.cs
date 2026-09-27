namespace Backgammon.AI
{
    using System;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Search;
    using Backgammon.Logic;

    /// <summary>
    /// What each level plays with. Level 6 is the strongest evaluator of the version, searching ahead through known
    /// stages. Levels 1..5 use the same evaluator with Gaussian noise on each play's match-winning chance, as GNU
    /// Backgammon does. The noise is calibrated in the arena (ARENA.md) so that each level is an even rating step above
    /// the one below.
    /// </summary>
    internal static class BotLevels
    {
        // Noise per level 1..6, in match-winning chance.
        private static readonly double[] Noise = { 0.30, 0.16, 0.09, 0.05, 0.02, 0 };

        public static IEvaluator EvaluatorFor(BackgammonVersion version) => BaselineEvaluator.Instance;

        public static SearchSettings SettingsFor(int level)
        {
            if (level < 1 || level > BackgammonBot.Levels)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, $"A level is 1..{BackgammonBot.Levels}.");
            }

            return new SearchSettings(Noise[level - 1], LookAhead: level == BackgammonBot.Levels ? 3 : 0, EvaluationBudget: 4_000);
        }
    }
}
