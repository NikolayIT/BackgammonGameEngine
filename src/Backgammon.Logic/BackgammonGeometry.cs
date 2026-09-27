namespace Backgammon.Logic
{
    using System;

    using Backgammon.Logic.Rules;

    /// <summary>
    /// How the two seats number the board. Every player numbers the points 1..24 in the direction it moves, so its
    /// checkers go from higher to lower numbers; steps are always in the mover's own numbering, and boards in views
    /// are in seat 0's.
    /// <list type="bullet">
    /// <item>Обикновена, тапа and челеби: the players move in opposite directions, and seat 1's point p is seat 0's
    /// 25 - p.</item>
    /// <item>Гюлбара: both move the same way round from diagonally opposite corners, and seat 1's point p is seat 0's
    /// ((p + 11) mod 24) + 1. So seat 1's 24 (its start) is seat 0's 12, and seat 1's home board (1..6) is seat 0's
    /// 13..18.</item>
    /// </list>
    /// </summary>
    public static class BackgammonGeometry
    {
        /// <summary>Converts a seat's own point number to seat 0's numbering.</summary>
        /// <param name="version">The rules of the game.</param>
        /// <param name="seat">The seat whose numbering <paramref name="point"/> is in: 0 or 1.</param>
        /// <param name="point">The point, 1..24.</param>
        /// <returns>The same point in seat 0's numbering.</returns>
        public static int ToSeat0(BackgammonVersion version, int seat, int point)
        {
            Check(seat, point);
            return Geometry.ToSeat0(version, seat, point);
        }

        /// <summary>Converts a point in seat 0's numbering to a seat's own numbering.</summary>
        /// <param name="version">The rules of the game.</param>
        /// <param name="seat">The seat whose numbering is wanted: 0 or 1.</param>
        /// <param name="seat0Point">The point in seat 0's numbering, 1..24.</param>
        /// <returns>The same point in <paramref name="seat"/>'s numbering.</returns>
        public static int FromSeat0(BackgammonVersion version, int seat, int seat0Point)
        {
            Check(seat, seat0Point);
            return Geometry.FromSeat0(version, seat, seat0Point);
        }

        /// <summary>Gets the board a game of <paramref name="version"/> starts from.</summary>
        /// <param name="version">The rules of the game.</param>
        /// <returns>The starting board, in seat 0's numbering.</returns>
        public static BackgammonBoard StartBoard(BackgammonVersion version) => ViewConverter.ToBoard(Position.Start(version));

        private static void Check(int seat, int point)
        {
            if (seat != 0 && seat != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "A seat is 0 or 1.");
            }

            if (point < 1 || point > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(point), point, "A point is 1..24.");
            }
        }
    }
}
