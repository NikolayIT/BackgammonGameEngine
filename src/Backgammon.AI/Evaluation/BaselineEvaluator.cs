namespace Backgammon.AI.Evaluation
{
    using System;

    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// The hand-written evaluator, and the reference a network must beat before it ships.
    /// <list type="bullet">
    /// <item>The race: pips measured in rolls, with the mean and spread of the dice, escalating doubles included.</item>
    /// <item>Hitting and pinning: blots and the rolls that hit them.</item>
    /// <item>Points: made points, primes, the home board and anchors.</item>
    /// <item>The bar and borne-off checkers.</item>
    /// <item>Тапа: pins and the mothers.</item>
    /// <item>Гюлбара: how badly each side blocks the other.</item>
    /// </list>
    /// All of it is mapped to the chances of a win and of 2-point results.
    /// </summary>
    internal sealed class BaselineEvaluator : IEvaluator
    {
        public static readonly BaselineEvaluator Instance = new();

        // Pips a roll moves: mean and spread, for ordinary rolls and when doubles escalate.
        private const double PlainMean = 8.1667;
        private const double PlainSpread = 4.3;
        private const double EscalatingMean = 15.94;
        private const double EscalatingSpread = 21.9;

        // Rolls out of 36 that move a checker exactly d pips (1..24) with one die or a combination, ignoring blocks.
        private static readonly int[] Shots = { 0, 11, 12, 14, 15, 15, 17, 6, 6, 5, 3, 2, 3, 0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1 };

        private BaselineEvaluator()
        {
        }

        public Outcome Evaluate(in Position position, int onRoll, RollCounts rollsMade)
        {
            var me = onRoll;
            var them = 1 - onRoll;
            var ownPips = position.Pips(me);
            var theirPips = position.Pips(them);

            // With escalating doubles nearly every roll of the rest of the game escalates.
            var escalates = Geometry.HasEscalation(position.Version);
            var mean = escalates ? EscalatingMean : PlainMean;
            var spread = escalates ? EscalatingSpread : PlainSpread;

            // The race in rolls: I need ownPips / mean rolls, they need theirPips / mean, and I roll first.
            var ownRolls = ownPips / mean;
            var theirRolls = theirPips / mean;
            var raceSpread = Math.Sqrt(((ownRolls + theirRolls) * (spread / mean) * (spread / mean)) + 0.25);
            var race = Normal((theirRolls - ownRolls + 0.5) / raceSpread);

            var contact = HasContact(position, me);
            double score;
            switch (position.Version)
            {
                case BackgammonVersion.Gyulbara:
                    score = (0.8 * Logit(race)) + Blocking(position, me) - Blocking(position, them);
                    break;
                case BackgammonVersion.Tapa:
                    score = contact ? (0.6 * Logit(race)) + TapaPositional(position, me) : Logit(race);
                    break;
                default:
                    score = contact ? (0.6 * Logit(race)) + HittingPositional(position, me) : Logit(race);
                    break;
            }

            if (position.Version == BackgammonVersion.Tapa)
            {
                score += Mothers(position, me);
            }

            var win = Sigmoid(score);

            // A 2-point result: the loser still has everything on the board when the winner is done.
            var ownFinish = ownRolls + (contact ? 3 : 0);
            var theirFinish = theirRolls + (contact ? 3 : 0);
            var winDouble = position.Count(them, Geometry.Off) > 0 ? 0 : win * Normal((RollsToFirstOff(position, them, mean) - ownFinish - 0.5) / raceSpread);
            var loseDouble = position.Count(me, Geometry.Off) > 0 ? 0 : (1 - win) * Normal((RollsToFirstOff(position, me, mean) - theirFinish + 0.5) / raceSpread);
            if (position.Version == BackgammonVersion.Tapa)
            {
                // A майка is worth two points as well.
                winDouble = Math.Max(winDouble, win * MotherShare(position, me));
                loseDouble = Math.Max(loseDouble, (1 - win) * MotherShare(position, them));
            }

            return Outcome.FromNetwork(win, Math.Min(winDouble, win), Math.Min(loseDouble, 1 - win));
        }

        /// <summary>Whether the sides can still hit, pin or block each other (in гюлбара they always can).</summary>
        private static bool HasContact(in Position position, int me)
        {
            if (position.Version == BackgammonVersion.Gyulbara)
            {
                return true;
            }

            // Opposite directions: contact while my rearmost checker is behind their rearmost one, in my numbering.
            var ownBack = 0;
            for (var point = Geometry.Bar; point >= 1; point--)
            {
                if (position.Count(me, point) > 0)
                {
                    ownBack = point;
                    break;
                }
            }

            var them = 1 - me;
            var theirBack = position.Count(them, Geometry.Bar) > 0 ? 0 : 25;
            for (var point = 1; point <= 24; point++)
            {
                if (position.Count(them, point) > 0)
                {
                    theirBack = Math.Min(theirBack, Geometry.Other(position.Version, point));
                }
            }

            return ownBack > theirBack;
        }

        /// <summary>Обикновена and челеби: blots and shots, points, primes, the bar.</summary>
        private static double HittingPositional(in Position position, int me)
        {
            var them = 1 - me;
            var version = position.Version;
            var score = 0.0;

            // Their blots I may hit now (I am on roll), and my blots they may hit after my turn.
            for (var point = 1; point <= 24; point++)
            {
                if (position.Count(them, point) == 1)
                {
                    var mine = Geometry.Other(version, point);
                    score += HitChance(position, me, mine) * (0.35 + (point / 30.0));
                }

                if (position.Count(me, point) == 1)
                {
                    score -= 0.75 * HitChance(position, them, Geometry.Other(version, point)) * (0.35 + (point / 30.0));
                }
            }

            score += 0.11 * (MadePoints(position, me, 1, 6) - MadePoints(position, them, 1, 6));
            score += 0.07 * (MadePoints(position, me, 19, 24) - MadePoints(position, them, 19, 24));
            score += 0.12 * (Math.Max(0, LongestRun(position, me) - 2) - Math.Max(0, LongestRun(position, them) - 2));
            score -= 0.45 * position.Count(me, Geometry.Bar) * (0.4 + (MadePoints(position, them, 1, 6) / 6.0));
            score += 0.5 * position.Count(them, Geometry.Bar) * (0.4 + (MadePoints(position, me, 1, 6) / 6.0));
            score -= 0.02 * (Stacking(position, me) - Stacking(position, them));
            return score;
        }

        /// <summary>Тапа: pins held and suffered, blots that can be pinned, points made.</summary>
        private static double TapaPositional(in Position position, int me)
        {
            var them = 1 - me;
            var version = position.Version;
            var score = 0.0;
            for (var point = 1; point <= 24; point++)
            {
                // A pinned checker is out of play: the further it still had to go, the worse.
                if (position.IsPinned(them, point) && point != 24)
                {
                    score += 0.45 + (point / 40.0);
                }

                if (position.IsPinned(me, point) && point != 24)
                {
                    score -= 0.45 + (point / 40.0);
                }

                if (position.Count(them, point) == 1 && !position.IsPinned(them, point) && point != 24)
                {
                    score += PinChance(position, me, Geometry.Other(version, point)) * (0.3 + (point / 40.0));
                }

                if (position.Count(me, point) == 1 && !position.IsPinned(me, point) && point != 24)
                {
                    score -= 0.7 * PinChance(position, them, Geometry.Other(version, point)) * (0.3 + (point / 40.0));
                }
            }

            score += 0.06 * (MadePoints(position, me, 1, 18) - MadePoints(position, them, 1, 18));
            score -= 0.015 * (Stacking(position, me) - Stacking(position, them));
            return score;
        }

        /// <summary>Тапа's mothers: the threat of a майка, and how exposed each mother is.</summary>
        private static double Mothers(in Position position, int me)
        {
            var them = 1 - me;
            var score = 0.0;
            if (position.Count(them, 24) == 1 && !position.IsPinned(them, 24))
            {
                // Their mother is alone on my 1 point; pinning it wins at once if my own start is empty.
                var chance = PinChance(position, me, 1);
                score += chance * (position.Count(me, 24) == 0 ? 3.0 : 0.8);
            }

            if (position.Count(me, 24) == 1 && !position.IsPinned(me, 24))
            {
                // My mother alone: they pin it after my turn unless I move it (I am on roll, so it may escape).
                var chance = PinChance(position, them, 1);
                score -= 0.5 * chance * (position.Count(them, 24) == 0 ? 3.0 : 0.8);
            }

            if (position.IsPinned(them, 24))
            {
                score += 1.5;
            }

            if (position.IsPinned(me, 24))
            {
                score -= 1.5;
            }

            return score;
        }

        /// <summary>The share of wins that are a майка: high when the mother is pinned or can be pinned now.</summary>
        private static double MotherShare(in Position position, int winner)
        {
            var loser = 1 - winner;
            if (position.IsPinned(loser, 24))
            {
                return 0.95;
            }

            return position.Count(loser, 24) == 1 ? 0.4 * PinChance(position, winner, 1) : 0;
        }

        /// <summary>Гюлбара: how much the other side's checkers are hemmed in by <paramref name="blocker"/>'s points.</summary>
        private static double Blocking(in Position position, int blocker)
        {
            var mover = 1 - blocker;
            var version = position.Version;
            var blocked = 0.0;
            for (var point = 7; point <= 24; point++)
            {
                var checkers = position.Count(mover, point);
                if (checkers == 0)
                {
                    continue;
                }

                // Of the six points in front of the checker, how many does the blocker hold?
                var held = 0;
                for (var step = 1; step <= 6 && point - step >= 1; step++)
                {
                    if (position.Count(blocker, Geometry.Other(version, point - step)) > 0)
                    {
                        held++;
                    }
                }

                blocked += checkers * held * held / 36.0;
            }

            return 0.25 * blocked;
        }

        /// <summary>The chance that <paramref name="shooter"/> can land on its own point <paramref name="target"/> in one roll.</summary>
        private static double HitChance(in Position position, int shooter, int target)
        {
            var miss = 1.0;
            for (var point = target + 1; point <= Geometry.Bar; point++)
            {
                if (position.Count(shooter, point) == 0 || position.IsPinned(shooter, point))
                {
                    continue;
                }

                var distance = point - target;
                if (distance < Shots.Length)
                {
                    miss *= 1 - (Shots[distance] / 36.0);
                }
            }

            return 1 - miss;
        }

        private static double PinChance(in Position position, int shooter, int target) => HitChance(position, shooter, target);

        private static int MadePoints(in Position position, int seat, int from, int to)
        {
            var made = 0;
            for (var point = from; point <= to; point++)
            {
                if (position.Count(seat, point) >= 2)
                {
                    made++;
                }
            }

            return made;
        }

        private static int LongestRun(in Position position, int seat)
        {
            int best = 0, run = 0;
            for (var point = 1; point <= 24; point++)
            {
                run = position.Count(seat, point) >= 2 ? run + 1 : 0;
                best = Math.Max(best, run);
            }

            return best;
        }

        private static int Stacking(in Position position, int seat)
        {
            var extra = 0;
            for (var point = 1; point <= 24; point++)
            {
                extra += Math.Max(0, position.Count(seat, point) - 3);
            }

            return extra;
        }

        /// <summary>Rolls <paramref name="seat"/> needs to bring every checker home and bear the first one off.</summary>
        private static double RollsToFirstOff(in Position position, int seat, double mean)
        {
            if (position.Count(seat, Geometry.Off) > 0)
            {
                return 0;
            }

            var pips = 0;
            var lowest = 6;
            for (var point = 1; point <= Geometry.Bar; point++)
            {
                var checkers = position.Count(seat, point);
                if (checkers == 0)
                {
                    continue;
                }

                if (point > 6)
                {
                    pips += (point - 6) * checkers;
                }
                else
                {
                    lowest = Math.Min(lowest, point);
                }
            }

            return (pips + lowest) / mean;
        }

        private static double Logit(double p)
        {
            p = Math.Clamp(p, 1e-6, 1 - 1e-6);
            return Math.Log(p / (1 - p));
        }

        private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

        // The standard normal distribution function (Abramowitz and Stegun 7.1.26 through erf).
        private static double Normal(double z)
        {
            var x = Math.Abs(z) / Math.Sqrt(2);
            var t = 1 / (1 + (0.3275911 * x));
            var erf = 1 - (((((((((1.061405429 * t) - 1.453152027) * t) + 1.421413741) * t) - 0.284496736) * t) + 0.254829592) * t * Math.Exp(-x * x));
            return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
        }
    }
}
