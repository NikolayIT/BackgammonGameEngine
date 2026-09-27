namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The full record of a match: every game's opening dice, rolls and steps. It is enough to replay the match exactly
    /// and to check every die against the dice source (see <see cref="BackgammonMatchOptions.Dice"/> for the draw
    /// order).
    /// </summary>
    public sealed class BackgammonMatchRecord
    {
        /// <summary>The version of the rules and of the draw order this engine plays by.</summary>
        public const int CurrentRulesVersion = 1;

        /// <summary>Gets the version of the rules and of the draw order the match was played by.</summary>
        public int RulesVersion { get; init; } = CurrentRulesVersion;

        /// <summary>Gets what the match was played as.</summary>
        public BackgammonVariant Variant { get; init; }

        /// <summary>Gets the points needed to win the match (while leading).</summary>
        public int TargetPoints { get; init; }

        /// <summary>Gets every game in order, the current one included.</summary>
        public IReadOnlyList<BackgammonGameRecord> Games { get; init; } = Array.Empty<BackgammonGameRecord>();

        /// <summary>Gets each seat's points.</summary>
        public IReadOnlyList<int> Scores { get; init; } = Array.Empty<int>();

        /// <summary>Gets the seat that won the match, or -1 while it goes on or when it was stopped.</summary>
        public int Winner { get; init; } = -1;

        /// <summary>Gets a value indicating whether the match was stopped (a resignation or a timeout) rather than finished.</summary>
        public bool IsStopped { get; init; }
    }
}
