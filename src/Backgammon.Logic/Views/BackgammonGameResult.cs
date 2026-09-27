namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>How one game of a match ended.</summary>
    public sealed class BackgammonGameResult
    {
        /// <summary>Gets the game's number in the match, counting from 1.</summary>
        public int GameNumber { get; init; }

        /// <summary>Gets the rules the game was played by.</summary>
        public BackgammonVersion Version { get; init; }

        /// <summary>
        /// Gets the winning seat, or -1 when nobody won (<see cref="BackgammonResultKind.Draw"/> or
        /// <see cref="BackgammonResultKind.Stuck"/>).
        /// </summary>
        public int Winner { get; init; } = -1;

        /// <summary>Gets the points each seat scored in this game.</summary>
        public IReadOnlyList<int> Points { get; init; } = Array.Empty<int>();

        /// <summary>Gets how the game ended.</summary>
        public BackgammonResultKind Kind { get; init; }
    }
}
