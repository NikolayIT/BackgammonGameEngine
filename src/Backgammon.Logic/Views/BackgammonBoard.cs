namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The checkers in a view: the 24 points in seat 0's numbering, and the bar and borne-off checkers of each seat.
    /// Seat 1 numbers the points its own way: in обикновена, тапа and челеби its point p is seat 0's 25 - p; in гюлбара
    /// it is seat 0's ((p + 11) mod 24) + 1 (see <see cref="BackgammonGeometry"/>).
    /// </summary>
    public sealed class BackgammonBoard
    {
        /// <summary>Gets the 24 points; index i is the point numbered i + 1 by seat 0.</summary>
        public IReadOnlyList<BackgammonPoint> Points { get; init; } = Array.Empty<BackgammonPoint>();

        /// <summary>Gets the checkers of each seat on the bar (only обикновена and челеби have a bar).</summary>
        public IReadOnlyList<int> Bar { get; init; } = Array.Empty<int>();

        /// <summary>Gets the checkers each seat has borne off.</summary>
        public IReadOnlyList<int> Off { get; init; } = Array.Empty<int>();
    }
}
