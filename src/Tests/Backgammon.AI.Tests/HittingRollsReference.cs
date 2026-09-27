namespace Backgammon.AI.Tests
{
    using System;

    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// The plain first version of <c>FeatureEncoder.HittingRolls</c>, which the layout-2 networks were trained with.
    /// The encoder's faster version must give exactly the same counts.
    /// </summary>
    internal static class HittingRollsReference
    {
        public static int Of(in Position position, int shooter)
        {
            var version = position.Version;
            if (version == BackgammonVersion.Gyulbara)
            {
                return 0;
            }

            var target = 1 - shooter;
            var tapa = version == BackgammonVersion.Tapa;
            var blot = new bool[26];
            var open = new bool[26];
            var any = false;
            for (var point = 1; point <= 24; point++)
            {
                var other = Geometry.Other(version, point);
                var theirs = position.Count(target, other);
                open[point] = theirs <= 1 && !(tapa && position.IsPinned(shooter, point));
                blot[point] = open[point] && theirs == 1 && !(tapa && position.IsPinned(target, other));
                any |= blot[point];
            }

            if (!any)
            {
                return 0;
            }

            var rolls = 0;
            for (var first = 1; first <= 6; first++)
            {
                for (var second = first; second <= 6; second++)
                {
                    if (Hits(position, shooter, blot, open, first, second))
                    {
                        rolls += first == second ? 1 : 2;
                    }
                }
            }

            return rolls;
        }

        private static bool Hits(in Position position, int shooter, bool[] blot, bool[] open, int first, int second)
        {
            var bar = position.Count(shooter, Geometry.Bar);
            if (first == second)
            {
                var steps = 4;
                if (bar > 0)
                {
                    var entry = Geometry.Bar - first;
                    if (!open[entry])
                    {
                        return false;
                    }

                    if (blot[entry])
                    {
                        return true;
                    }

                    steps -= bar;
                    if (steps <= 0)
                    {
                        return false;
                    }

                    if (Reaches(entry, first, steps, blot, open))
                    {
                        return true;
                    }
                }

                for (var from = 24; from >= 1; from--)
                {
                    if (Movable(position, shooter, from) && Reaches(from, first, steps, blot, open))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (bar >= 2)
            {
                return blot[Geometry.Bar - first] || blot[Geometry.Bar - second];
            }

            if (bar == 1)
            {
                return EntersThenHits(position, shooter, blot, open, first, second) || EntersThenHits(position, shooter, blot, open, second, first);
            }

            for (var from = 24; from >= 1; from--)
            {
                if (!Movable(position, shooter, from))
                {
                    continue;
                }

                if (Lands(blot, from - first) || Lands(blot, from - second))
                {
                    return true;
                }

                var both = from - first - second;
                if (Lands(blot, both) && (open[from - first] || open[from - second]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EntersThenHits(in Position position, int shooter, bool[] blot, bool[] open, int enter, int then)
        {
            var entry = Geometry.Bar - enter;
            if (!open[entry])
            {
                return false;
            }

            if (blot[entry] || Lands(blot, entry - then))
            {
                return true;
            }

            for (var from = 24; from >= 1; from--)
            {
                if (Movable(position, shooter, from) && Lands(blot, from - then))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Reaches(int from, int die, int steps, bool[] blot, bool[] open)
        {
            for (var step = 1; step <= steps; step++)
            {
                var to = from - (step * die);
                if (to < 1 || !open[to])
                {
                    return false;
                }

                if (blot[to])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Lands(bool[] blot, int to) => to >= 1 && blot[to];

        private static bool Movable(in Position position, int seat, int from) => position.Count(seat, from) > 0 && !position.IsPinned(seat, from);
    }
}
