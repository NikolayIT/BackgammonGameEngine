namespace Backgammon.AI
{
    using System;

    using Backgammon.AI.Search;
    using Backgammon.Logic;

    /// <summary>
    /// Computer players for табла, six levels for every version.
    /// <list type="bullet">
    /// <item>A bot decides from a seat view alone: the board, the stage to play and the stages known to follow, whose
    /// roll it is and the match score.</item>
    /// <item>It never sees the dice stream, and only its own <see cref="Random"/> (the noise of the weaker levels).</item>
    /// <item>For a given view, level and <see cref="Random"/> state it always makes the same decision.</item>
    /// <item>A decision takes well under 20 ms on one core.</item>
    /// <item>It is thread-safe: any number of decisions may run at once.</item>
    /// </list>
    /// </summary>
    public static class BackgammonBot
    {
        /// <summary>The number of levels, 1 (weakest) to 6 (strongest).</summary>
        public const int Levels = 6;

        /// <summary>Chooses how the seat to move plays its stage.</summary>
        /// <param name="view">A view in which a seat is to move, typically that seat's own view.</param>
        /// <param name="level">The level, 1..6.</param>
        /// <param name="random">The bot's own source of randomness (only the weaker levels use it).</param>
        /// <returns>The action to play.</returns>
        public static BackgammonAction Choose(BackgammonSeatView view, int level, Random random) => Decide(view, level, random).Action;

        /// <summary>Chooses how the seat to move plays its stage, and says how hard the choice was.</summary>
        /// <param name="view">A view in which a seat is to move.</param>
        /// <param name="level">The level, 1..6.</param>
        /// <param name="random">The bot's own source of randomness.</param>
        /// <returns>The decision.</returns>
        public static BackgammonBotDecision Decide(BackgammonSeatView view, int level, Random random)
        {
            ArgumentNullException.ThrowIfNull(random);
            if (level < 1 || level > Levels)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, $"A level is 1..{Levels}.");
            }

            var situation = Situation.FromView(view);
            var settings = BotLevels.SettingsFor(level, situation.Position.Version);
            var (ends, choice) = Chooser.Current.Choose(situation, BotLevels.EvaluatorFor(situation.Position.Version), settings, random);
            return new BackgammonBotDecision
            {
                Action = new BackgammonAction { Steps = ends[choice].Steps.ToSteps() },
                Complexity = ComplexityOf(ends.Count),
                DistinctPlays = ends.Count,
            };
        }

        /// <summary>
        /// How hard the stage to play is, from the number of distinct positions it can end in: 0 when the play is
        /// forced, 1 for about 16 of them (a typical roll), rising slowly beyond (about 1.6 for 100) up to 3. A host can
        /// scale a bot's think time by it.
        /// </summary>
        /// <param name="view">A view in which a seat is to move.</param>
        /// <returns>The complexity, 0..3.</returns>
        public static double Complexity(BackgammonSeatView view) => ComplexityOf(BackgammonStageMoves.For(view).OutcomeCount);

        internal static double ComplexityOf(int distinctPlays) => distinctPlays <= 1 ? 0 : Math.Min(3, Math.Log(1 + distinctPlays) / Math.Log(17));
    }
}
