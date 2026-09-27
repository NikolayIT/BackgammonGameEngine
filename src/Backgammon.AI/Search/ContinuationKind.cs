namespace Backgammon.AI.Search
{
    /// <summary>What happens after a play of the current stage that does not end the game.</summary>
    internal enum ContinuationKind
    {
        /// <summary>The turn is over and the opponent rolls.</summary>
        OpponentRolls,

        /// <summary>The mover has finished a remainder and rolls itself.</summary>
        MoverRolls,

        /// <summary>The mover plays more known stages: the rest of its escalating chain or of a remainder.</summary>
        OwnStages,

        /// <summary>The mover's chain breaks: the opponent plays the rest, then rolls.</summary>
        OpponentRemainder,
    }
}
