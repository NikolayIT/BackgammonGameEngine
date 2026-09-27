namespace Backgammon.Logic
{
    /// <summary>One point of the board in a view, numbered as seat 0 numbers it.</summary>
    public sealed class BackgammonPoint
    {
        /// <summary>Gets the point's number in seat 0's numbering, 1..24.</summary>
        public int Number { get; init; }

        /// <summary>Gets how many of seat 0's checkers are on the point (a pinned one included).</summary>
        public int Seat0 { get; init; }

        /// <summary>Gets how many of seat 1's checkers are on the point (a pinned one included).</summary>
        public int Seat1 { get; init; }

        /// <summary>
        /// Gets the seat whose single checker is pinned here, under the other seat's checkers (тапа), or -1 when no
        /// checker is pinned. A point holds both seats' checkers only when one is pinned.
        /// </summary>
        public int PinnedSeat { get; init; } = -1;
    }
}
