namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One stage as it was played: by whom, with which dice, and which steps. Every stage that starts is recorded,
    /// including a stage with no legal move (no steps, played by the engine).
    /// </summary>
    public sealed class BackgammonPlay
    {
        /// <summary>
        /// Gets the play's number in the match, counting from 1 and never repeating, so a host can compare it with the
        /// last one it showed.
        /// </summary>
        public int Ply { get; init; }

        /// <summary>Gets the seat that played the stage.</summary>
        public int Seat { get; init; }

        /// <summary>
        /// Gets the roll the stage comes from: two dice in the order drawn. For the opening roll it is seat 0's die then
        /// seat 1's. For a remainder it is the roll of the player who passed it on.
        /// </summary>
        public IReadOnlyList<int> Roll { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Gets which of its roller's rolls in this game it came from, counting from 1. The starter's opening roll is its
        /// 1st.
        /// </summary>
        public int RollNumber { get; init; }

        /// <summary>
        /// Gets the stage's position in its chain, counting from 0: 0 for the rolled dice themselves, then 1, 2, … for the
        /// later stages of an escalating chain or of a remainder.
        /// </summary>
        public int Stage { get; init; }

        /// <summary>Gets the stage's dice: two different dice, or one to four equal ones.</summary>
        public IReadOnlyList<int> Dice { get; init; } = Array.Empty<int>();

        /// <summary>Gets the steps played, in the player's own numbering and in order.</summary>
        public IReadOnlyList<BackgammonStep> Steps { get; init; } = Array.Empty<BackgammonStep>();

        /// <summary>
        /// Gets a value indicating whether the engine played the stage, because it had no legal move or because it was
        /// forced and auto-play is on.
        /// </summary>
        public bool Auto { get; init; }

        /// <summary>Gets a value indicating whether the stage is part of a remainder passed on by the opponent.</summary>
        public bool IsRemainder { get; init; }

        /// <summary>Gets a value indicating whether the stage is the game's opening roll.</summary>
        public bool IsOpeningRoll { get; init; }
    }
}
