namespace Backgammon.AI
{
    using System;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Neural;
    using Backgammon.AI.Search;
    using Backgammon.Logic;

    /// <summary>
    /// What each level plays with. Level 6 is the strongest evaluator of the version, searching ahead through known
    /// stages and one roll deeper at its best plays. Levels 1..5 use the same evaluator without the search, and with
    /// Gaussian noise on each play's match-winning chance, as GNU Backgammon does. The noise is calibrated in the arena (ARENA.md) so that each level is an even rating step above
    /// the one below.
    /// </summary>
    internal static class BotLevels
    {
        // Noise per version and level 1..6, in match-winning chance, from the arena's calibrate command (ARENA.md):
        // level 1 wins 75% of matches against random play, and the levels above it are even rating steps up to level 6.
        private static readonly double[][] Noise =
        {
            new[] { 0.3283, 0.1324, 0.0726, 0.041, 0.0207, 0 }, // обикновена: steps of 313 Elo
            new[] { 0.0593, 0.0334, 0.0204, 0.012, 0.0048, 0 }, // гюлбара: 116 Elo
            new[] { 0.4283, 0.1387, 0.0705, 0.0387, 0.0175, 0 }, // тапа: 305 Elo
            new[] { 0.0734, 0.0444, 0.026, 0.015, 0.0046, 0 }, // челеби: 71 Elo
        };

        private static readonly Lazy<IEvaluator>[] Evaluators =
        {
            new(() => Create(BackgammonVersion.Obiknovena)),
            new(() => Create(BackgammonVersion.Gyulbara)),
            new(() => Create(BackgammonVersion.Tapa)),
            new(() => Create(BackgammonVersion.Chelebi)),
        };

        /// <summary>The strongest evaluator of a version: its shipped network, or the hand-written baseline.</summary>
        public static IEvaluator EvaluatorFor(BackgammonVersion version) => Evaluators[(int)version].Value;

        public static SearchSettings SettingsFor(int level, BackgammonVersion version)
        {
            if (level < 1 || level > BackgammonBot.Levels)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, $"A level is 1..{BackgammonBot.Levels}.");
            }

            // Level 6 plays on through the known stages of a chain and looks one roll deeper at its two best plays.
            return level == BackgammonBot.Levels
                ? new SearchSettings(0, LookAhead: 3, EvaluationBudget: 3_000, OneRoll: 2)
                : new SearchSettings(Noise[(int)version][level - 1], LookAhead: 0, EvaluationBudget: 0);
        }

        private static IEvaluator Create(BackgammonVersion version) =>
            Networks.For(version) is { } network ? new NeuralEvaluator(network) : BaselineEvaluator.Instance;
    }
}
