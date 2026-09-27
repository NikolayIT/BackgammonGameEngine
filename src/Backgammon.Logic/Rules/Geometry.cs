namespace Backgammon.Logic.Rules
{
    /// <summary>
    /// How the two players' numberings of the board relate. Each player numbers the points 1..24 in the direction it
    /// moves, so its checkers always travel from higher to lower numbers and are borne off below 1; 25 is the bar
    /// and 0 is off.
    /// </summary>
    internal static class Geometry
    {
        public const int Off = 0;
        public const int Bar = 25;
        public const int Slots = 26;
        public const int Checkers = 15;

        // For every own point 1..24, the same point in the other player's numbering. Both maps are involutions,
        // so the same table serves both seats.
        private static readonly byte[] Opposite = BuildOpposite();
        private static readonly byte[] Diagonal = BuildDiagonal();

        /// <summary>
        /// The other player's number for <paramref name="point"/> (1..24) of this player. In обикновена, тапа and
        /// челеби the players move in opposite directions, so point p is the other's 25 - p. In гюлбара both move the
        /// same way round and the start points are diagonally opposite, so point p is the other's ((p + 11) mod 24) + 1:
        /// seat 1's 24 is seat 0's 12, and seat 1's home (1..6) is seat 0's 13..18.
        /// </summary>
        public static int Other(BackgammonVersion version, int point) =>
            version == BackgammonVersion.Gyulbara ? Diagonal[point] : Opposite[point];

        /// <summary>The seat-0 number of <paramref name="point"/> (1..24) in <paramref name="seat"/>'s numbering.</summary>
        public static int ToSeat0(BackgammonVersion version, int seat, int point) =>
            seat == 0 ? point : Other(version, point);

        /// <summary><paramref name="seat"/>'s number for the seat-0 point <paramref name="seat0Point"/> (1..24).</summary>
        public static int FromSeat0(BackgammonVersion version, int seat, int seat0Point) =>
            seat == 0 ? seat0Point : Other(version, seat0Point);

        public static bool HasBar(BackgammonVersion version) =>
            version == BackgammonVersion.Obiknovena || version == BackgammonVersion.Chelebi;

        public static bool HasEscalation(BackgammonVersion version) =>
            version == BackgammonVersion.Gyulbara || version == BackgammonVersion.Chelebi;

        private static byte[] BuildOpposite()
        {
            var map = new byte[Slots];
            for (var point = 1; point <= 24; point++)
            {
                map[point] = (byte)(25 - point);
            }

            return map;
        }

        private static byte[] BuildDiagonal()
        {
            var map = new byte[Slots];
            for (var point = 1; point <= 24; point++)
            {
                map[point] = (byte)(((point + 11) % 24) + 1);
            }

            return map;
        }
    }
}
