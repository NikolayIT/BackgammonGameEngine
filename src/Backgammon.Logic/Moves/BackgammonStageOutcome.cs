namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>One distinct position the current stage can end in, and the canonical steps that reach it.</summary>
    public sealed class BackgammonStageOutcome
    {
        /// <summary>Gets the board after the stage, in seat 0's numbering.</summary>
        public BackgammonBoard Board { get; init; } = new();

        /// <summary>
        /// Gets the canonical steps to this end, in the mover's numbering: of all the legal ways to reach it, the first in
        /// the canonical order (the higher point first, then the larger die). The engine plays these when it plays a
        /// stage itself.
        /// </summary>
        public IReadOnlyList<BackgammonStep> Steps { get; init; } = Array.Empty<BackgammonStep>();

        /// <summary>Gets a value indicating whether the stage ends the game (the last checker off, a майка, or both mothers pinned).</summary>
        public bool EndsGame { get; init; }
    }
}
