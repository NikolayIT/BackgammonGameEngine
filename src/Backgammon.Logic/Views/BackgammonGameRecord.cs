namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>Everything that happened in one game: the opening dice, every play, and the result.</summary>
    public sealed class BackgammonGameRecord
    {
        /// <summary>Gets the game's number in the match, counting from 1.</summary>
        public int GameNumber { get; init; }

        /// <summary>Gets the rules the game was played by.</summary>
        public BackgammonVersion Version { get; init; }

        /// <summary>
        /// Gets the opening pairs in the order drawn, each as seat 0's die and then seat 1's. Every pair but the last is a
        /// tie.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<int>> Openings { get; init; } = Array.Empty<IReadOnlyList<int>>();

        /// <summary>Gets the seat that won the opening and played first.</summary>
        public int Starter { get; init; }

        /// <summary>Gets every play of the game, in order.</summary>
        public IReadOnlyList<BackgammonPlay> Plays { get; init; } = Array.Empty<BackgammonPlay>();

        /// <summary>
        /// Gets the roll drawn for the turn in play that has no play yet, as two dice in the order drawn, or empty. Every
        /// die drawn is accounted for by the openings, the plays and this roll, even in a match stopped mid-turn.
        /// </summary>
        public IReadOnlyList<int> PendingRoll { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Gets how the game ended, or null while it goes on (or when the match was stopped during it).
        /// </summary>
        public BackgammonGameResult? Result { get; init; }
    }
}
