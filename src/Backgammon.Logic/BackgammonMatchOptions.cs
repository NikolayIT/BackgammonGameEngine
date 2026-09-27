namespace Backgammon.Logic
{
    using System;

    /// <summary>How a <see cref="BackgammonMatch"/> is played.</summary>
    public sealed class BackgammonMatchOptions
    {
        /// <summary>The purpose passed to <see cref="Dice"/> for the dice of the opening roll.</summary>
        public const string OpeningPurpose = "opening";

        /// <summary>The purpose passed to <see cref="Dice"/> for the dice of every other roll.</summary>
        public const string DicePurpose = "dice";

        /// <summary>Gets what the match is played as: one version for every game, or the среща rotation.</summary>
        public BackgammonVariant Variant { get; init; } = BackgammonVariant.Obiknovena;

        /// <summary>
        /// Gets the points a player needs to win the match, which it must reach while leading. Null means the traditional
        /// target: 5 for a среща, 3 for everything else.
        /// </summary>
        public int? TargetPoints { get; init; }

        /// <summary>
        /// Gets the source of every die: called with n = 6 and a purpose (<see cref="OpeningPurpose"/> or
        /// <see cref="DicePurpose"/>), it returns a uniformly distributed integer 0..5, and the die is that plus one.
        /// The order of the calls is exactly this, and the rules use no other randomness:
        /// <list type="number">
        /// <item>Each game starts with pairs of "opening" draws, seat 0's die first and then seat 1's, until the two
        /// differ. The seat with the higher die starts and plays both dice.</item>
        /// <item>Every later roll is two "dice" draws, made when that roll starts, which is after the previous turn
        /// (and any remainder played before it) is over.</item>
        /// <item>Escalating doubles and remainders draw nothing, and nothing is drawn once the match is over.</item>
        /// </list>
        /// Null means <see cref="Random.Shared"/>.
        /// </summary>
        public Func<int, string, int>? Dice { get; init; }

        /// <summary>
        /// Gets a value indicating whether a stage whose legal moves all lead to the same position is played by the
        /// engine (its canonical steps, marked as automatic). A stage with no legal move is always passed by the engine.
        /// </summary>
        public bool AutoPlayForcedStages { get; init; }

        /// <summary>
        /// Gets a value indicating whether the match keeps its history: the plays in the views, the previous game, and
        /// the record. Bots and simulations that only need the position can turn it off.
        /// </summary>
        public bool RecordHistory { get; init; } = true;
    }
}
