namespace Backgammon.AI.Neural
{
    using System;

    using Backgammon.AI.Evaluation;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// Turns a quiet position into the network's inputs, from the point of view of the seat about to roll ("me"). The
    /// layout is the same for every version (inputs a version does not use stay 0). The inputs are sparse: they are
    /// written as index and value pairs, in increasing index order.
    /// <list type="table">
    /// <item><term>0..95</term><description>My checkers on each of my points 1..24, four units a point, in TD-Gammon's
    /// truncated unary form: at least 1, at least 2, at least 3, and (n - 3) / 2 beyond.</description></item>
    /// <item><term>96..191</term><description>The opponent's checkers on each of my points, the same way.</description></item>
    /// <item><term>192..215</term><description>Тапа: my checker pinned on my point p.</description></item>
    /// <item><term>216..239</term><description>Тапа: an opponent checker pinned on my point p.</description></item>
    /// <item><term>240..243</term><description>The bar (count / 2) and borne-off checkers (count / 15), mine and theirs.</description></item>
    /// <item><term>244..246</term><description>Pip counts / 200, mine and theirs, and their difference / 100.</description></item>
    /// <item><term>247..248</term><description>Whether my next roll escalates, and whether theirs does.</description></item>
    /// <item><term>249</term><description>Whether the checkers can still meet (always so in гюлбара).</description></item>
    /// <item><term>250..253</term><description>Тапа mothers: mine alone on my start, theirs alone, my start empty, theirs empty.</description></item>
    /// <item><term>254..255</term><description>Тапа: a pinned checker keeps me, or them, from bearing off.</description></item>
    /// </list>
    /// </summary>
    internal static class FeatureEncoder
    {
        public const int Inputs = 256;

        /// <summary>The layout's version, stored with the weights so a network is never read with another layout.</summary>
        public const int Layout = 1;

        /// <summary>The most inputs that can be non-zero at once.</summary>
        public const int MaxActive = 256;

        public static int Encode(in Position position, int onRoll, RollCounts rolls, Span<int> indices, Span<float> values)
        {
            var me = onRoll;
            var them = 1 - onRoll;
            var version = position.Version;
            var count = 0;

            for (var point = 1; point <= 24; point++)
            {
                count = Unary(position.Count(me, point), (point - 1) * 4, indices, values, count);
            }

            for (var point = 1; point <= 24; point++)
            {
                count = Unary(position.Count(them, Geometry.Other(version, point)), 96 + ((point - 1) * 4), indices, values, count);
            }

            if (position.HasPins)
            {
                for (var point = 1; point <= 24; point++)
                {
                    if (position.IsPinned(me, point))
                    {
                        count = Add(192 + point - 1, 1, indices, values, count);
                    }
                }

                for (var point = 1; point <= 24; point++)
                {
                    if (position.IsPinned(them, Geometry.Other(version, point)))
                    {
                        count = Add(216 + point - 1, 1, indices, values, count);
                    }
                }
            }

            count = Add(240, position.Count(me, Geometry.Bar) / 2f, indices, values, count);
            count = Add(241, position.Count(them, Geometry.Bar) / 2f, indices, values, count);
            count = Add(242, position.Count(me, Geometry.Off) / 15f, indices, values, count);
            count = Add(243, position.Count(them, Geometry.Off) / 15f, indices, values, count);
            var ownPips = position.Pips(me);
            var theirPips = position.Pips(them);
            count = Add(244, ownPips / 200f, indices, values, count);
            count = Add(245, theirPips / 200f, indices, values, count);
            count = Add(246, (theirPips - ownPips) / 100f, indices, values, count);
            count = Add(247, rolls.NextEscalates(position, me) ? 1 : 0, indices, values, count);
            count = Add(248, rolls.NextEscalates(position, them) ? 1 : 0, indices, values, count);
            count = Add(249, HasContact(position, me) ? 1 : 0, indices, values, count);
            if (version == BackgammonVersion.Tapa)
            {
                count = Add(250, position.Count(me, 24) == 1 && !position.IsPinned(me, 24) ? 1 : 0, indices, values, count);
                count = Add(251, position.Count(them, 24) == 1 && !position.IsPinned(them, 24) ? 1 : 0, indices, values, count);
                count = Add(252, position.Count(me, 24) == 0 ? 1 : 0, indices, values, count);
                count = Add(253, position.Count(them, 24) == 0 ? 1 : 0, indices, values, count);
                count = Add(254, position.PinnedMask(me) != 0 ? 1 : 0, indices, values, count);
                count = Add(255, position.PinnedMask(them) != 0 ? 1 : 0, indices, values, count);
            }

            return count;
        }

        /// <summary>Whether the two sides' checkers can still meet: always in гюлбара, else while mine are behind theirs.</summary>
        public static bool HasContact(in Position position, int me)
        {
            if (position.Version == BackgammonVersion.Gyulbara)
            {
                return true;
            }

            var own = 0;
            for (var point = Geometry.Bar; point >= 1; point--)
            {
                if (position.Count(me, point) > 0)
                {
                    own = point;
                    break;
                }
            }

            var them = 1 - me;
            var theirs = position.Count(them, Geometry.Bar) > 0 ? 0 : 25;
            for (var point = 24; point >= 1; point--)
            {
                if (position.Count(them, point) > 0)
                {
                    theirs = Math.Min(theirs, Geometry.Other(position.Version, point));
                    break;
                }
            }

            return own > theirs;
        }

        private static int Unary(int checkers, int offset, Span<int> indices, Span<float> values, int count)
        {
            if (checkers == 0)
            {
                return count;
            }

            count = Add(offset, 1, indices, values, count);
            if (checkers >= 2)
            {
                count = Add(offset + 1, 1, indices, values, count);
            }

            if (checkers >= 3)
            {
                count = Add(offset + 2, 1, indices, values, count);
            }

            if (checkers > 3)
            {
                count = Add(offset + 3, (checkers - 3) / 2f, indices, values, count);
            }

            return count;
        }

        private static int Add(int index, float value, Span<int> indices, Span<float> values, int count)
        {
            if (value == 0)
            {
                return count;
            }

            indices[count] = index;
            values[count] = value;
            return count + 1;
        }
    }
}
