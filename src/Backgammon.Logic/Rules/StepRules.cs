namespace Backgammon.Logic.Rules
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The single-step rules: whether one checker may move by one die, and what moving it does. A step is given in
    /// the mover's own numbering: <c>from</c> is 1..24 for a point or 25 for the bar, and the checker goes to
    /// <c>from - die</c>, where 0 or below means borne off.
    /// </summary>
    internal static class StepRules
    {
        public static bool CanStep(in Position position, int seat, int from, int die)
        {
            if ((uint)(die - 1) > 5 || (uint)(from - 1) > 24 || position.Count(seat, from) == 0)
            {
                return false;
            }

            if (from != Geometry.Bar && position.Count(seat, Geometry.Bar) > 0)
            {
                // A checker on the bar must re-enter before any other move.
                return false;
            }

            if (position.IsPinned(seat, from))
            {
                // Тапа: a pinned checker is always alone on its point, so nothing there can move.
                return false;
            }

            var to = from - die;
            return to >= 1 ? CanLand(position, seat, to) : CanBearOff(position, seat, from, to);
        }

        /// <summary>Moves a checker; the caller has checked <see cref="CanStep"/>.</summary>
        public static GameEnd Apply(ref Position position, int seat, int from, int die)
        {
            var version = position.Version;
            var opponent = 1 - seat;
            position.Add(seat, from, -1);
            if (version == BackgammonVersion.Tapa && position.Count(seat, from) == 0)
            {
                // The last checker leaving a point releases the opponent's checker it was pinning there.
                position.SetPinned(opponent, Geometry.Other(version, from), false);
            }

            var to = from - die;
            if (to < 1)
            {
                position.Add(seat, Geometry.Off, 1);
                return position.Count(seat, Geometry.Off) == Geometry.Checkers ? GameEnd.BorneOff : GameEnd.None;
            }

            var other = Geometry.Other(version, to);
            if (position.Count(opponent, other) == 1)
            {
                if (version == BackgammonVersion.Tapa)
                {
                    // A lone checker is pinned; one that is already pinned (by the mover) just gets company.
                    position.SetPinned(opponent, other, true);
                }
                else
                {
                    // Обикновена and челеби hit a lone checker to the bar (гюлбара never lands on one).
                    position.Set(opponent, other, 0);
                    position.Add(opponent, Geometry.Bar, 1);
                }
            }

            position.Add(seat, to, 1);
            return version == BackgammonVersion.Tapa ? MotherEnd(position, seat) : GameEnd.None;
        }

        /// <summary>
        /// Тапа, after a step by <paramref name="seat"/>: both mothers pinned is a draw; the opponent's mother pinned
        /// while the mover has no checker on its own start is the mover's win.
        /// </summary>
        public static GameEnd MotherEnd(in Position position, int seat)
        {
            var opponent = 1 - seat;
            if (!position.IsPinned(opponent, 24))
            {
                return GameEnd.None;
            }

            if (position.IsPinned(seat, 24))
            {
                return GameEnd.BothMothers;
            }

            return position.Count(seat, 24) == 0 ? GameEnd.Mother : GameEnd.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanLand(in Position position, int seat, int to)
        {
            var version = position.Version;
            var opponentCount = position.Count(1 - seat, Geometry.Other(version, to));
            switch (version)
            {
                case BackgammonVersion.Gyulbara:
                    // One checker holds a point.
                    return opponentCount == 0;

                case BackgammonVersion.Tapa:
                    // My own checker pinned there means the opponent holds the point (a pinner cannot be pinned).
                    // Otherwise one opponent checker is either pinned by me (I stack) or lone (I pin it).
                    return !position.IsPinned(seat, to) && opponentCount <= 1;

                default:
                    return opponentCount <= 1;
            }
        }

        private static bool CanBearOff(in Position position, int seat, int from, int to)
        {
            // All 15 must be home: none on 7..24 or the bar.
            for (var point = 7; point <= Geometry.Bar; point++)
            {
                if (position.Count(seat, point) != 0)
                {
                    return false;
                }
            }

            if (position.PinnedMask(seat) != 0)
            {
                // Тапа: while one of your checkers is pinned in your home board you bear nothing off.
                return false;
            }

            if (to == 0)
            {
                return true;
            }

            // A higher die bears off only from the highest occupied point.
            for (var point = from + 1; point <= 6; point++)
            {
                if (position.Count(seat, point) != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
