namespace Backgammon.AI.Evaluation
{
    using Backgammon.Logic.Rules;

    /// <summary>
    /// Values a position that is quiet: nothing is being played, and <c>onRoll</c> is about to roll. Implementations
    /// are immutable and thread-safe.
    /// </summary>
    internal interface IEvaluator
    {
        /// <summary>The chances from the point of view of the seat about to roll.</summary>
        /// <param name="position">The position.</param>
        /// <param name="onRoll">The seat about to roll.</param>
        /// <param name="rollsMade">How many rolls each seat has made in this game (for escalating doubles).</param>
        /// <returns>How the game is likely to end for <paramref name="onRoll"/>.</returns>
        Outcome Evaluate(in Position position, int onRoll, RollCounts rollsMade);
    }
}
