namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A player's action: how it plays the current stage, as the list of steps in the order they are made. A stage is
    /// the two dice of a roll, the four equal dice of a double, one group of four of an escalating chain, or one stage
    /// of a remainder. Every die that can be played must be played (see RULES.md); a stage with no legal move is
    /// passed by the engine itself and never needs an action.
    /// </summary>
    public sealed class BackgammonAction
    {
        /// <summary>Gets the steps, in the mover's own numbering, in the order they are made.</summary>
        public IReadOnlyList<BackgammonStep> Steps { get; init; } = Array.Empty<BackgammonStep>();

        /// <summary>Creates an action from its steps.</summary>
        /// <param name="steps">The steps in the order they are made.</param>
        /// <returns>The action.</returns>
        public static BackgammonAction Of(params BackgammonStep[] steps) => new() { Steps = steps };

        /// <inheritdoc/>
        public override string ToString() => string.Join(' ', this.Steps);
    }
}
