namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What a seat sees of a match: the board, the dice in play, the score and this game's plays. A plain model with
    /// no future dice (the next roll is drawn only when it starts), so it can be sent to a player as it is. A bot
    /// decides from it alone.
    /// </summary>
    public sealed class BackgammonSeatView
    {
        /// <summary>Gets the seat the view is for, or -1 for the final view.</summary>
        public int Seat { get; init; } = -1;

        /// <summary>Gets what the match is played as.</summary>
        public BackgammonVariant Variant { get; init; }

        /// <summary>Gets the rules of the current (or last) game.</summary>
        public BackgammonVersion Version { get; init; }

        /// <summary>Gets the current (or last) game's number, counting from 1.</summary>
        public int GameNumber { get; init; }

        /// <summary>Gets the points needed to win the match (while leading).</summary>
        public int TargetPoints { get; init; }

        /// <summary>Gets each seat's points.</summary>
        public IReadOnlyList<int> Scores { get; init; } = Array.Empty<int>();

        /// <summary>Gets the seat to act, or -1 when nobody is (the match is over).</summary>
        public int ToMove { get; init; } = -1;

        /// <summary>Gets a value indicating whether the match is over: finished or stopped.</summary>
        public bool IsMatchFinished { get; init; }

        /// <summary>Gets a value indicating whether the match was stopped (a resignation or a timeout).</summary>
        public bool IsStopped { get; init; }

        /// <summary>Gets the seat that won the match, or -1 while it goes on or when it was stopped.</summary>
        public int MatchWinner { get; init; } = -1;

        /// <summary>
        /// Gets the number of the last play made in the match (see <see cref="BackgammonPlay.Ply"/>), or 0 before the first.
        /// </summary>
        public int Ply { get; init; }

        /// <summary>Gets the checkers.</summary>
        public BackgammonBoard Board { get; init; } = new();

        /// <summary>Gets each seat's pip count, in its own numbering (the bar counting 25).</summary>
        public IReadOnlyList<int> Pips { get; init; } = Array.Empty<int>();

        /// <summary>Gets the roll in play: two dice in the order drawn, or empty when nothing is in play.</summary>
        public IReadOnlyList<int> Roll { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Gets the seat whose roll is in play, or -1. It differs from <see cref="ToMove"/> while a remainder is being
        /// played.
        /// </summary>
        public int RollSeat { get; init; } = -1;

        /// <summary>Gets which of its roller's rolls in this game the roll in play is, counting from 1.</summary>
        public int RollNumber { get; init; }

        /// <summary>
        /// Gets the dice of the stage to play now: two different dice, or one to four equal ones. An action plays this
        /// stage.
        /// </summary>
        public IReadOnlyList<int> StageDice { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Gets the stage's position in its chain, counting from 0 (see <see cref="BackgammonPlay.Stage"/>).
        /// </summary>
        public int StageNumber { get; init; }

        /// <summary>
        /// Gets the stages that follow the current one in the escalating chain or remainder in play, in order. Empty
        /// for an ordinary roll.
        /// </summary>
        public IReadOnlyList<BackgammonStage> ChainRest { get; init; } = Array.Empty<BackgammonStage>();

        /// <summary>
        /// Gets a value indicating whether the roll in play is an escalating double. If its roller cannot play all of
        /// it, the rest passes to the opponent.
        /// </summary>
        public bool IsEscalating { get; init; }

        /// <summary>Gets a value indicating whether <see cref="ToMove"/> is playing a remainder passed on by the opponent.</summary>
        public bool IsPlayingRemainder { get; init; }

        /// <summary>Gets how many rolls each seat has made in this game (the starter's opening roll counts as one).</summary>
        public IReadOnlyList<int> RollsMade { get; init; } = Array.Empty<int>();

        /// <summary>Gets a value per seat indicating whether a double in its next roll would escalate (гюлбара and челеби, from the 4th roll).</summary>
        public IReadOnlyList<bool> NextRollEscalates { get; init; } = Array.Empty<bool>();

        /// <summary>
        /// Gets how many rolls in a row have moved no checker. At <see cref="BackgammonMatch.StuckRollLimit"/> the game
        /// ends with no points.
        /// </summary>
        public int StuckRolls { get; init; }

        /// <summary>Gets this game's plays so far, in order (empty when the match does not record history).</summary>
        public IReadOnlyList<BackgammonPlay> Plays { get; init; } = Array.Empty<BackgammonPlay>();

        /// <summary>
        /// Gets the previous game's record, or null in the first game (or without history). A host can use it to show
        /// how that game ended after the next one has started.
        /// </summary>
        public BackgammonGameRecord? LastGame { get; init; }

        /// <summary>Gets the result of every finished game, in order.</summary>
        public IReadOnlyList<BackgammonGameResult> Results { get; init; } = Array.Empty<BackgammonGameResult>();

        /// <summary>Gets the full record of the match; only in the final view (<see cref="BackgammonMatch.GetFinalView"/>).</summary>
        public BackgammonMatchRecord? Record { get; init; }
    }
}
