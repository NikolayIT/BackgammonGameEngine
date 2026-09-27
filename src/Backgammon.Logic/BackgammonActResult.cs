namespace Backgammon.Logic
{
    /// <summary>What <see cref="BackgammonMatch.Act"/> (and <see cref="BackgammonMatch.Validate"/>) says about an action.</summary>
    public enum BackgammonActResult
    {
        /// <summary>The action is legal; <see cref="BackgammonMatch.Act"/> has played it.</summary>
        Ok = 0,

        /// <summary>It is not this seat's turn (nothing changed).</summary>
        NotYourTurn = 1,

        /// <summary>The action is not a legal way to play the current stage (nothing changed).</summary>
        Illegal = 2,

        /// <summary>The match is over, finished or stopped (nothing changed).</summary>
        MatchFinished = 3,
    }
}
